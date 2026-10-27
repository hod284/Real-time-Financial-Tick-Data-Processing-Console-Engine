using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;

namespace ConsoleApp1.Telegram
{
    // 대시보드에 보여줄 상태 (토큰은 절대 안 넣음)
    //  State: "미설정" / "메시지 대기" / "연결됨"
    public sealed record TelegramStatus(string State, string? BotName);

    // 텔레그램 설정 보관소
    //  - 대시보드에서 토큰 입력 → 진짜인지 확인 → telegram.local.json에 저장
    //  - ChatId는 사용자가 봇에게 처음 보낸 메시지에서 TelegramReceiver가 알아내서 SetChatId로 넣음
    //  - 바뀌면 ChangeToken을 취소 → TelegramReceiver가 새 봇으로 다시 수신 시작
    //  - 웹 요청 스레드와 텔레그램 수신 스레드가 같이 쓰므로 lock
    public sealed class TelegramSettings
    {
        public const string FileName = "telegram.local.json";
        private static string FilePath => Path.Combine(AppContext.BaseDirectory, FileName);

        private sealed record SavedFile(string BotToken, long ChatId);

        private readonly object _lock = new();
        private ITelegramBotClient? _client;
        private string? _token;
        private string? _botName;
        private long _chatId;
        private CancellationTokenSource _changeCts = new();

        public TelegramSettings(IConfiguration config)
        {
            // 우선순위: telegram.local.json(대시보드에서 저장한 것) > AppSetting.json
            // AppSetting은 IOptions 대신 직접 읽음 → ChatId가 "" 처럼 비어 있어도 시작이 안 죽게
            var saved = Load();
            string? fileToken = config["Telegram:BotToken"];
            if (saved is { BotToken.Length: > 0 })
            {
                _token = saved.BotToken;
                _chatId = saved.ChatId;
            }
            else if (!string.IsNullOrWhiteSpace(fileToken))
            {
                _token = fileToken.Trim();
                long.TryParse(config["Telegram:ChatId"], out _chatId);   // 비었거나 숫자가 아니면 0
            }

            if (_token is not null)
                _client = new TelegramBotClient(_token);
        }

        public long ChatId
        {
            get { lock (_lock) return _chatId; }
        }

        // 지금 봇 + "설정이 바뀌면 취소되는 토큰"을 한 번에 (둘이 어긋나지 않게)
        public (ITelegramBotClient? Client, CancellationToken Changed) Current()
        {
            lock (_lock) return (_client, _changeCts.Token);
        }

        public TelegramStatus Status
        {
            get
            {
                lock (_lock)
                {
                    string state = _client is null ? "미설정"
                                 : _chatId == 0   ? "메시지 대기"
                                 :                  "연결됨";
                    return new TelegramStatus(state, _botName);
                }
            }
        }

        // 대시보드에서 토큰 입력 → 텔레그램에 확인 → 저장 (ChatId는 새로 받음)
        public async Task<(bool Ok, string Message)> ConnectAsync(string token, CancellationToken ct)
        {
            token = token.Trim();
            if (token.Length == 0)
                return (false, "토큰을 입력하세요");

            var client = new TelegramBotClient(token);
            string? name;
            try
            {
                name = (await client.GetMe(ct)).Username;   // 토큰이 진짜인지 확인
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return (false, $"토큰 확인 실패: {ex.Message}");
            }

            lock (_lock)
            {
                _client = client;
                _token = token;
                _botName = name;
                _chatId = 0;
                Save();
            }
            NotifyChanged();
            return (true, $"@{name} 확인됨. 텔레그램에서 이 봇에게 아무 메시지나 보내세요.");
        }

        // TelegramReceiver가 첫 메시지를 받으면 호출 → 그 채팅방이 알림 받을 곳
        public void SetChatId(long chatId)
        {
            lock (_lock)
            {
                _chatId = chatId;
                Save();
            }
        }

        public void SetBotName(string? name)
        {
            lock (_lock) _botName = name;
        }

        // 대시보드 [연결 해제]
        public void Disconnect()
        {
            lock (_lock)
            {
                _client = null;
                _token = null;
                _botName = null;
                _chatId = 0;
            }
            try { File.Delete(FilePath); } catch (IOException) { }
            NotifyChanged();
        }

        // 기다리고 있는 TelegramReceiver를 깨움 (예전 토큰 취소 → 새 토큰으로 교체)
        private void NotifyChanged()
        {
            CancellationTokenSource old;
            lock (_lock)
            {
                old = _changeCts;
                _changeCts = new CancellationTokenSource();
            }
            old.Cancel();
        }

        // lock 안에서만 호출
        private void Save()
        {
            if (_token is null)
                return;
            var json = JsonSerializer.Serialize(new SavedFile(_token, _chatId),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }

        private static SavedFile? Load()
        {
            try
            {
                return File.Exists(FilePath)
                    ? JsonSerializer.Deserialize<SavedFile>(File.ReadAllText(FilePath))
                    : null;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Telegram] {FileName} 읽기 실패: {ex.Message}");
                return null;
            }
        }
    }
}
