using ConsoleApp1.Adoator;
using ConsoleApp1.Analysis;
using ConsoleApp1.Event;
using ConsoleApp1.Health;
using ConsoleApp1.Models;
using ConsoleApp1.Storage;
using ConsoleApp1.Store;
using Microsoft.Extensions.Hosting;
using System.Threading.Channels;

namespace ConsoleApp1.singleton
{
    //  [BinanceFeed]─┐                          ┌ health.OnEvent()     수신 상태
    //                ├▶ ingest 채널 ▶ 디스패처 ─┼ store.Update(e)       최신 값 (화면 조회용)
    //  [UpbiteFeed]──┘                          ├ dbWriter.Enqueue(e)  DB 저장
    //        ↑                                  └ engine.Process(e) → Alert → bus.Publish
    //        └ subscriptions.Changed → Reconnect()                      ├ 콘솔 / DB
    //                                                                   └ 대시보드 / 텔레그램
    public sealed class MarketBotHostedService(
        Database database,
        SymbolDirectory directory,
        SubscriptionService subscriptions,
        FeedHealthCheck health,
        MarketStore store,
        DbWriter dbWriter,
        AnalysisEngine engine,
        EventBus bus,
        IEnumerable<IAlertObserver> observers) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            database.Initialize();
            await directory.LoadAsync(stoppingToken);   // 한글명 ↔ 코드 목록
            subscriptions.Load();                       // DB 구독 목록 (첫 실행이면 AppSetting 기본값)

            var ingest = Channel.CreateUnbounded<MarketEvent>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

            // 뒤에서 따로 도는 작업들
            var writerTask = dbWriter.RunAsync();
            var senders = observers.OfType<QueuedAlertObserver>().ToList();          // 대시보드, 텔레그램
            var senderTasks = senders.Select(s => s.RunAsync(CancellationToken.None)).ToList();
            var dispatchTask = Task.Run(() => DispatchAsync(ingest.Reader));

            // 피드는 연결할 때마다 "지금 구독 목록"을 다시 읽음
            var binance = new BinanceFeed(() => subscriptions.CodesOf(Exchange.Binance));
            var upbit = new UpbiteFeed(() => subscriptions.CodesOf(Exchange.Upbit));

            // 구독이 바뀌면 그 거래소만 재연결
            subscriptions.Changed += ex =>
                (ex == Exchange.Binance ? (WebSocketFeedBase)binance : upbit).Reconnect();

            var feeds = new[]
            {
                binance.RunAsync(ingest.Writer, stoppingToken),
                upbit.RunAsync(ingest.Writer, stoppingToken),
            };

            try
            {
                await Task.WhenAll(feeds);              // Ctrl+C 전까지 계속 돎
            }
            finally
            {
                ingest.Writer.TryComplete();            // 1) 수신 끝
                await dispatchTask;                     // 2) 남은 이벤트 처리
                dbWriter.Complete();                    // 3) 저장 큐 닫기
                senders.ForEach(s => s.Complete());     // 4) 전송 큐 닫기
                await Task.WhenAll(senderTasks.Append(writerTask));   // 5) 남은 것 처리 대기
                Console.WriteLine("정상 종료");
            }
        }

        // 디스패처: 이 루프 하나만 store와 engine에 씀 (그래서 lock 없이 안전)
        private async Task DispatchAsync(ChannelReader<MarketEvent> reader)
        {
            await foreach (var e in reader.ReadAllAsync())
            {
                health.OnEvent(e.Exchange, e.RecvTimeMs);   // 뭐든 오면 "살아 있음"

                if (e.Symbol.Length == 0)
                    continue;

                store.Update(e);                            // 최신 값 (화면 조회용)
                dbWriter.Enqueue(e);                        // DB 저장

                try
                {
                    foreach (var alert in engine.Process(e))   // 분석 (+ SymbolState 복사본 저장)
                        bus.Publish(alert);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"분석 실패: {e.Exchange} {e.Symbol} {ex.Message}");
                }
            }
        }
    }
}