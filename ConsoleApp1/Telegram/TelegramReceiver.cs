using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ConsoleApp1.Telegram
{
    // 텔레그램 메시지·버튼 받기 (롱 폴링)
    //  토큰이 없으면: 대시보드에서 입력할 때까지 대기
    //  ChatId가 없으면: 봇에게 처음 메시지를 보낸 채팅을 "내 채팅"으로 등록
    //  글자: "바이낸스"/"업비트" → 구독 목록 버튼, 그 외 → 거래소 선택 버튼
    //  버튼: "val|trade|Upbit|KRW-BTC" → key="val", args=["trade","Upbit","KRW-BTC"]
    //  보안: 등록된 ChatId에서 온 것만 처리
    public sealed class TelegramReceiver(
        TelegramSettings settings,
        IEnumerable<ICommand> commands) : BackgroundService
    {
        private readonly Dictionary<string, ICommand> _commands = commands.ToDictionary(c => c.Key);

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                // 지금 봇 + 설정 변경 신호. 대시보드에서 토큰을 바꾸거나 해제하면 linked가 취소됨
                var (bot, changed) = settings.Current();
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, changed);
                try
                {
                    if (bot is null)
                    {
                        await Task.Delay(Timeout.Infinite, linked.Token);   // 토큰 입력될 때까지 대기
                        continue;
                    }
                    await PollAsync(bot, linked.Token);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;                                                  // 프로그램 종료
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("[Telegram] 설정 변경 → 다시 시작");   // 새 토큰으로 다시
                }
            }
        }

        private async Task PollAsync(ITelegramBotClient bot, CancellationToken ct)
        {
            try
            {
                settings.SetBotName((await bot.GetMe(ct)).Username);         // 대시보드에 봇 이름 표시용
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.Error.WriteLine($"[Telegram] 봇 확인 실패: {ex.Message}");
            }

            Console.WriteLine("[Telegram] 수신 시작");
            int offset = 0;   // 어디까지 읽었는지
            while (true)
            {
                Update[] updates;
                try
                {
                    // 새 메시지가 올 때까지 최대 30초 기다렸다가 돌아옴 (롱 폴링)
                    updates = await bot.GetUpdates(offset: offset, timeout: 30, cancellationToken: ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Console.Error.WriteLine($"[Telegram] 수신 실패: {ex.Message}");
                    await Task.Delay(5000, ct);
                    continue;
                }

                foreach (var update in updates)
                {
                    offset = update.Id + 1;
                    try
                    {
                        await HandleUpdateAsync(bot, update, ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Console.Error.WriteLine($"[Telegram] 처리 실패: {ex.Message}");
                    }
                }
            }
        }

        private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
        {
            long fromChat = update.CallbackQuery is { } c ? (c.Message?.Chat.Id ?? c.From.Id)
                          : update.Message?.Chat.Id ?? 0;
            if (fromChat == 0)
                return;

            // ChatId가 아직 없으면: 처음 메시지를 보낸 채팅을 주인으로 등록
            long myChatId = settings.ChatId;
            if (myChatId == 0)
            {
                settings.SetChatId(fromChat);
                Console.WriteLine($"[Telegram] ChatId {fromChat} 등록");
                await bot.SendMessage(fromChat,
                    "✅ 연결됐어요. 이제 이 채팅으로 알림이 와요.\n'바이낸스' 또는 '업비트'를 입력해 보세요.",
                    cancellationToken: ct);
                return;
            }
            if (fromChat != myChatId)
                return;   // 다른 사람은 무시

            // ① 버튼을 누른 경우
            if (update.CallbackQuery is { } cq)
            {
                await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct);   // 버튼의 "로딩 중" 표시 끄기
                var parts = (cq.Data ?? "").Split('|');
                await ReplyAsync(bot, fromChat, Run(parts[0], parts[1..]), ct);
                return;
            }

            // ② 글자를 보낸 경우
            if (update.Message is { Text: { } text })
            {
                var (key, args) = ParseText(text);
                await ReplyAsync(bot, fromChat, Run(key, args), ct);
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

        private static Task ReplyAsync(ITelegramBotClient bot, long chatId, IMessage message, CancellationToken ct)
        {
            var reply = TelegramView.Render(message);
            return bot.SendMessage(chatId, reply.Text, replyMarkup: reply.Keyboard, cancellationToken: ct);
        }
    }
}
