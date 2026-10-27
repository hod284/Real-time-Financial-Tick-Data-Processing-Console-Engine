using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;

namespace ConsoleApp1.singleton
{
    // 처음 실행할 때 텔레그램 토큰/ChatId를 콘솔에서 받아 telegram.local.json에 저장
    //  - 토큰: 사용자가 붙여넣기 (BotFather에서 받은 것)
    //  - ChatId: 사용자가 봇에게 아무 메시지나 보내면 자동으로 알아냄
    //  - 다음 실행부터는 파일에서 읽음. 바꾸고 싶으면 파일 삭제
    //  - Program.cs에서 AddMarketBot보다 먼저 호출해야 함 (토큰 유무로 텔레그램 기능을 켜고 끄기 때문)
    public static class TelegramSetup
    {
        public const string FileName = "telegram.local.json";
        private static string FilePath => Path.Combine(AppContext.BaseDirectory, FileName);

        public static async Task EnsureAsync(ConfigurationManager config)
        {
            string? token = config["Telegram:BotToken"];
            long.TryParse(config["Telegram:ChatId"], out long chatId);

            if (!string.IsNullOrWhiteSpace(token) && chatId != 0)
                return;                                         // 이미 설정돼 있음

            Console.WriteLine();
            Console.WriteLine("=== 텔레그램 알림 설정 ===");

            // ① 토큰 입력 + 확인
            ITelegramBotClient? bot = null;
            string? botName = null;
            while (bot is null)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    Console.Write("봇 토큰을 붙여넣고 Enter (텔레그램 없이 쓰려면 그냥 Enter): ");
                    token = Console.ReadLine()?.Trim();
                    if (string.IsNullOrEmpty(token))
                    {
                        Console.WriteLine("→ 텔레그램 없이 실행합니다 (대시보드만 사용)\n");
                        return;
                    }
                }
                try
                {
                    var client = new TelegramBotClient(token);
                    var me = await client.GetMe();              // 토큰이 진짜인지 텔레그램에 확인
                    bot = client;
                    botName = me.Username;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"→ 토큰 확인 실패: {ex.Message}. 다시 입력하세요.");
                    token = null;
                }
            }

            // ② ChatId: 사용자가 봇에게 메시지를 보내면 그 채팅방 번호를 가져옴
            if (chatId == 0)
            {
                Console.WriteLine($"텔레그램에서 @{botName} 을 찾아 아무 메시지나 보내세요 (예: /start). 기다리는 중...");
                int offset = 0;
                while (chatId == 0)
                {
                    var updates = await bot.GetUpdates(offset: offset, timeout: 30);
                    foreach (var u in updates)
                    {
                        offset = u.Id + 1;
                        if (u.Message is { } m)
                            chatId = m.Chat.Id;
                    }
                }
                await bot.GetUpdates(offset: offset, timeout: 0);   // 읽은 메시지 확정 (TelegramReceiver가 다시 처리하지 않게)
                await bot.SendMessage(chatId, "✅ 연결됐어요. 이제 이 채팅으로 알림이 와요.");
                Console.WriteLine($"→ ChatId {chatId} 확인");
            }

            // ③ 파일에 저장 + 지금 실행에도 바로 반영
            var json = JsonSerializer.Serialize(
                new { Telegram = new { BotToken = token, ChatId = chatId } },
                new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(FilePath, json);
            Console.WriteLine($"→ 저장: {FilePath} (바꾸려면 이 파일을 지우고 다시 실행)\n");

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Telegram:BotToken"] = token,
                ["Telegram:ChatId"] = chatId.ToString(),
            });
        }
    }
}
