using ConsoleApp1.Event;
using ConsoleApp1.Models;
using System.Threading.Channels;
using Telegram.Bot;

namespace ConsoleApp1.Telegram
{
    // OnAlert는 큐에 넣기만 → 실제 전송은 RunAsync 루프가 (네트워크라 느려서)
    // 큐는 "보내기 전 대기 줄"일 뿐, 보내고 나면 사라짐
    public sealed class TelegramNotifier : IAlertObserver
    {
        private readonly Channel<Alert> _queue =
            Channel.CreateUnbounded<Alert>(new UnboundedChannelOptions 
            {
                 SingleReader = true 
            });
        private readonly ITelegramBotClient _bot;
        private readonly long _chatId;

        public TelegramNotifier(ITelegramBotClient bot, long chatId)
        {
            _bot = bot;
            _chatId = chatId;
        }

        public void OnAlert(Alert alert) 
        {
             _queue.Writer.TryWrite(alert);   // 바로 리턴
        }
        public void Complete() 
        {
             _queue.Writer.TryComplete();
        }
        public async Task RunAsync(CancellationToken ct)
        {
            await foreach (var alert in _queue.Reader.ReadAllAsync(ct))
            {
                try
                {
                    await _bot.SendMessage(_chatId, alert.Message, cancellationToken: ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Console.Error.WriteLine($"텔레그램 전송 실패: {ex.Message}");
                }
            }
        }
    }
}