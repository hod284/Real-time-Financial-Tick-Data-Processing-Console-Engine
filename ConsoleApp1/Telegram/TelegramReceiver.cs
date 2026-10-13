using ConsoleApp1.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ConsoleApp1.Telegram
{
    // 텔레그램 메시지·버튼 받기 (롱 폴링)
    //  글자: "바이낸스"/"업비트" → 구독 목록 버튼, 그 외 → 거래소 선택 버튼
    //  버튼: "val|trade|Upbit|KRW-BTC" → key="val", args=["trade","Upbit","KRW-BTC"]
    //  보안: 설정의 ChatId(내 채팅방)에서 온 것만 처리
    public sealed class TelegramReceiver(
        ITelegramBotClient bot,
        IOptions<TelegramOptions> options,
        IEnumerable<ICommand> commands) : BackgroundService
    {
        private readonly Dictionary<string, ICommand> _commands = commands.ToDictionary(c => c.Key);

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            long myChatId = options.Value.ChatId;
            int offset = 0;   // 어디까지 읽었는지

            while (!ct.IsCancellationRequested)
            {
                Update[] updates;
                try
                {
                    // 새 메시지가 올 때까지 최대 30초 기다렸다가 돌아옴 (롱 폴링)
                    updates = await bot.GetUpdates(offset: offset, timeout: 30, cancellationToken: ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Telegram] 수신 실패: {ex.Message}");
                    try 
                    { 
                        await Task.Delay(5000, ct); 
                    } 
                    catch (OperationCanceledException) 
                    {
                         break;
                          }
                    continue;
                }

                foreach (var update in updates)
                {
                    offset = update.Id + 1;
                    try
                    {
                        await HandleUpdateAsync(update, myChatId, ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Console.Error.WriteLine($"[Telegram] 처리 실패: {ex.Message}");
                    }
                }
            }
        }

        private async Task HandleUpdateAsync(Update update, long myChatId, CancellationToken ct)
        {
            // ① 버튼을 누른 경우
            if (update.CallbackQuery is { } cq)
            {
                long chatId = cq.Message?.Chat.Id ?? cq.From.Id;
                if (chatId != myChatId) return;

                await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct);   // 버튼의 "로딩 중" 표시 끄기
                var parts = (cq.Data ?? "").Split('|');
                await ReplyAsync(chatId, Run(parts[0], parts[1..]), ct);
                return;
            }

            // ② 글자를 보낸 경우
            if (update.Message is { Text: { } text } msg && msg.Chat.Id == myChatId)
            {
                var (key, args) = ParseText(text);
                await ReplyAsync(msg.Chat.Id, Run(key, args), ct);
            }
        }

        private static (string Key, string[] Args) ParseText(string text)
        {
            var t = text.Trim().ToLowerInvariant();
            if (t is "바이낸스" or "binance") return ("ex", new[] { "Binance" });
            if (t is "업비트" or "upbit")     return ("ex", new[] { "Upbit" });
            return ("ex", Array.Empty<string>());   // /start 포함 그 외 → 거래소 선택 버튼
        }

        private IMessage Run(string key, string[] args)
        {
            if (!_commands.TryGetValue(key, out var command))
                command = _commands["ex"];
            try
            {
                return command.Execute(args);
            }
            catch (Exception ex)
            {
                return new TextMessage($"처리 중 오류: {ex.Message}");
            }
        }

        private Task ReplyAsync(long chatId, IMessage message, CancellationToken ct)
        {
            var reply = TelegramView.Render(message);
            return bot.SendMessage(chatId, reply.Text, replyMarkup: reply.Keyboard, cancellationToken: ct);
        }
    }
}