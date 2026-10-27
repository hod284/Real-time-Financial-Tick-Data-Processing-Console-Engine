using ConsoleApp1.Event;
using ConsoleApp1.Models;
using Telegram.Bot;

namespace ConsoleApp1.Telegram
{
    // 알림 → 텔레그램 자동 전송 + [🔎 이 종목 보기] 버튼
    //  보내는 순간의 설정을 읽음 → 대시보드에서 토큰을 바꿔도 재시작 없이 반영
    //  아직 연결 전(토큰 없음 / ChatId 없음)이면 조용히 건너뜀
    public sealed class TelegramNotifier(TelegramSettings settings) : QueuedAlertObserver
    {
        protected override string Name => "Telegram";

        protected override Task SendAsync(Alert alert, CancellationToken ct)
        {
            var (bot, _) = settings.Current();
            long chatId = settings.ChatId;
            if (bot is null || chatId == 0)
                return Task.CompletedTask;

            return bot.SendMessage(chatId, alert.Message,
                replyMarkup: TelegramView.OpenSymbolButton(alert.Exchange, alert.Symbol),
                cancellationToken: ct);
        }
    }
}
