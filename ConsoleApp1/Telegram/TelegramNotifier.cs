using ConsoleApp1.Event;
using ConsoleApp1.Models;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace ConsoleApp1.Telegram
{
    // 알림 → 텔레그램 자동 전송 + [🔎 이 종목 보기] 버튼
    public sealed class TelegramNotifier(ITelegramBotClient bot, IOptions<TelegramOptions> options) : QueuedAlertObserver
    {
        private readonly long _chatId = options.Value.ChatId;

        protected override string Name => "Telegram";

        protected override Task SendAsync(Alert alert, CancellationToken ct)
            => bot.SendMessage(_chatId, alert.Message,
                   replyMarkup: TelegramView.OpenSymbolButton(alert.Exchange, alert.Symbol),
                   cancellationToken: ct);
    }
}