using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

namespace ConsoleApp1.Storage
{
    //  [디스패처] --Enqueue()--> [큐(Channel)] --1초마다 꺼냄--> [트랜잭션 1개로 INSERT] --> SQLite
    public sealed class DbWriter
    {
        // object 큐: 체결/캔들/티커/알림 등 타입이 섞여 들어옴
        private readonly Channel<object> _queue =
            Channel.CreateUnbounded<object>(new UnboundedChannelOptions { SingleReader = true });

        private readonly Database _db;
        private readonly TradeRepository _trades;
        private readonly CandleRepository _candles;
        private readonly TickerRepository _tickers;   // [선택] ticker_latest 안 쓰면 제거
        private readonly AlertRepository _alerts;
        private readonly ILogger<DbWriter> _logger;
        private readonly TimeSpan _interval;
        private readonly int _maxBatch;
// [선택] 10초마다 찍는 통계용 → 튜닝 끝나면 빼도 됨
        private long _statTrades; //저장한 체결 건수
        private long _statCandles;//저장한 캔들 건수
        private long _statTickers;//저장한 티커 건수
        private long _statAlerts; // 저장한 알림 건수
        private long _statFlushes;//커밋한 횟수 (트랜잭션 몇 번 했나)
        private long _statMaxBatch;// 한 번에 가장 많이 저장한 건수
        private long _lastStatMs = Environment.TickCount64;//	마지막으로 로그 찍은 시각

        public DbWriter(Database db, TradeRepository trades, CandleRepository candles, TickerRepository tickers,
                        AlertRepository alerts, IOptions<MarketOptions> options, ILogger<DbWriter> logger)
        {
            _db = db;
            _trades = trades;
            _candles = candles;
            _tickers = tickers;
            _alerts = alerts;
            _logger = logger;
            _interval = TimeSpan.FromMilliseconds(Math.Max(100, options.Value.DbWriter.FlushIntervalMs));
            _maxBatch = Math.Max(1, options.Value.DbWriter.MaxBatchSize);
        }

        public int Pending => _queue.Reader.Count;   // 아직 저장 안 된 건수

        // 넣기만 하고 바로 리턴 → 수신/분석 쪽이 DB 때문에 안 막힘
        public void Enqueue(MarketEvent e)
        {
            if (e is TradeEvent or CandleEvent or TickerEvent)   // 호가는 저장 안 함
                _queue.Writer.TryWrite(e);
        }

        public void Enqueue(Alert alert) 
        {
             _queue.Writer.TryWrite(alert);
        }
        // 종료 신호: 더 이상 안 넣음 → RunAsync가 남은 거 저장하고 끝남
        public void Complete() 
        {
             _queue.Writer.TryComplete();
        }
        
        public async Task RunAsync()
        {
            using var conn = _db.Open();               // 쓰기용 연결 하나를 계속 재사용
            using var timer = new PeriodicTimer(_interval);
            var batch = new List<object>(_maxBatch);

            while (true)
            {
                await timer.WaitForNextTickAsync();    // 1초 대기
                FlushAll(conn, batch);
                LogStatsIfDue();                       // [선택]

                // Complete() 됐고 큐도 비었으면 종료
                if (_queue.Reader.Completion.IsCompleted)
                    break;
            }
            _logger.LogInformation("DbWriter 종료: 남은 데이터 저장 완료");
        }

        // 지금 큐에 있는 걸 전부, 최대 500건씩 나눠서 저장
        private void FlushAll(SqliteConnection conn, List<object> batch)
        {
            while (true)
            {
                batch.Clear();
                while (batch.Count < _maxBatch && _queue.Reader.TryRead(out var item))
                    batch.Add(item);
                if (batch.Count == 0)
                    return;

                try
                {
                    SaveBatch(conn, batch);
                }
                catch (Exception ex)
                {
                    // DB 에러가 나도 프로그램은 계속 돌게, 이 배치만 버림
                    _logger.LogError(ex, "DB 저장 실패 - {Count}건 버림", batch.Count);
                }
            }
        }

        private void SaveBatch(SqliteConnection conn, List<object> batch)
        {
            using var tx = conn.BeginTransaction();   // ★ 핵심: 수백 건을 커밋 1번으로

            // 타입별로 나눔 + 파싱 실패로 빈 값인 건 거름
            var trades  = batch.OfType<TradeEvent>().Where(t => t.Price > 0 && t.Symbol.Length > 0).ToList();
            var candles = batch.OfType<CandleEvent>().Where(c => c.OpenTimeMs > 0 && c.Symbol.Length > 0).ToList();
            var tickers = batch.OfType<TickerEvent>().Where(t => t.LastPrice > 0 && t.Symbol.Length > 0).ToList();
            var alerts  = batch.OfType<Alert>().ToList();

            if (trades.Count > 0)  _trades.Insert(conn, tx, trades);
            if (candles.Count > 0) _candles.Upsert(conn, tx, candles);
            if (tickers.Count > 0) _tickers.Upsert(conn, tx, tickers);
            if (alerts.Count > 0)  _alerts.Insert(conn, tx, alerts);

            tx.Commit();   // 여기서 한 번에 디스크에 확정

            // [선택] 통계
            _statTrades += trades.Count;
            _statCandles += candles.Count;
            _statTickers += tickers.Count;
            _statAlerts += alerts.Count;
            _statFlushes++;
            _statMaxBatch = Math.Max(_statMaxBatch, batch.Count);
        }

        // [선택] 몇 건씩 저장되는지 10초마다 로그 → FlushIntervalMs / MaxBatchSize 조절할 때 참고
        private void LogStatsIfDue()
        {
            long now = Environment.TickCount64;
            if (now - _lastStatMs < 10_000)
                return;
            long total = _statTrades + _statCandles + _statTickers + _statAlerts;
            _logger.LogInformation(
                "DB 저장 (최근 10초): {Total}건 [체결 {Trades}, 캔들 {Candles}, 티커 {Tickers}, 알림 {Alerts}] / 커밋 {Flushes}회, 최대 배치 {Max}건, 대기 {Pending}건",
                total, _statTrades, _statCandles, _statTickers, _statAlerts, _statFlushes, _statMaxBatch, Pending);
            _statTrades = _statCandles = _statTickers = _statAlerts = _statFlushes = _statMaxBatch = 0;
            _lastStatMs = now;
        }
    
    }
}