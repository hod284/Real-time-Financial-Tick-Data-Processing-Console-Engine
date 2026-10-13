using ConsoleApp1.Models;
using ConsoleApp1.singleton;
using ConsoleApp1.Storage;
using Microsoft.Extensions.Options;

namespace ConsoleApp1.Store
{
    // 구독 목록 관리 (메모리 + DB)
    //  - 읽기: 피드(연결할 때), 웹, 텔레그램
    //  - 쓰기: 웹에서 추가/삭제 → DB 저장 + Changed로 피드 재연결
    //  - 여러 스레드가 만지므로 lock
    public sealed class SubscriptionService(
        SubscriptionRepository repo,
        SymbolDirectory directory,
        IOptions<MarketOptions> options)
    {
        private readonly object _lock = new();
        private List<Subscription> _items = new();

        // 구독이 바뀌면 알려줌 → HostedService가 해당 거래소 피드를 Reconnect()
        public event Action<Exchange>? Changed;

        // 시작할 때 1번: DB에서 불러오고, 비어 있으면(첫 실행) AppSetting.json 기본 종목으로 채움
        public void Load()
        {
            var saved = repo.GetAll();
            if (saved.Count > 0)
            {
                lock (_lock) 
                 _items = saved.ToList();
                return;
            }
            foreach (var s in options.Value.BinanceSymbols) Add(Exchange.Binance, s, out _, notify: false);
            foreach (var c in options.Value.UpbitCodes) Add(Exchange.Upbit, c, out _, notify: false);
        }

        public IReadOnlyList<Subscription> GetAll()
        {
            lock (_lock) return _items.ToList();   // 복사본 (밖에서 순회 중에 바뀌지 않게)
        }

        public IReadOnlyList<string> CodesOf(Exchange ex)
        {
            lock (_lock) return _items.Where(s => s.Exchange == ex).Select(s => s.Symbol).ToList();
        }

        public bool TryAdd(Exchange ex, string input, out string message)
            => Add(ex, input, out message, notify: true);

        private bool Add(Exchange ex, string input, out string message, bool notify)
        {
            var code = directory.Resolve(ex, input);
            if (code is null)
            {
                message = $"{ex}에서 '{input}' 종목을 찾을 수 없어요";
                return false;
            }

            var sub = new Subscription(ex, code, directory.FindName(ex, code) ?? code);
            lock (_lock)
            {
                if (_items.Any(s => s.Exchange == ex && s.Symbol == code))
                {
                    message = $"{sub.Name}({code})는 이미 구독 중이에요";
                    return false;
                }
                _items.Add(sub);
            }
            repo.Add(sub);

            message = $"{sub.Name}({code}) 구독 추가";
            if (notify) Changed?.Invoke(ex);
            return true;
        }

        public bool Remove(Exchange ex, string symbol)
        {
            int removed;
            lock (_lock)
                removed = _items.RemoveAll(s => s.Exchange == ex && s.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
                return false;

            repo.Remove(ex, symbol.ToUpperInvariant());
            Changed?.Invoke(ex);
            return true;
        }
// 거래소 + 코드로 정확히 찾기 (텔레그램 버튼, 웹 상세 화면)
        public Subscription? Find(Exchange ex, string symbol)
        {
            lock (_lock)
                return _items.FirstOrDefault(s =>
                    s.Exchange == ex && s.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
        }
        // 구독 중인 것 중에서 찾기: "비트코인", "BTC", "KRW-BTC", "BTCUSDT" 다 됨
        public IReadOnlyList<Subscription> Match(string input)
        {
            var key = input.Trim();
            lock (_lock)
                return _items.Where(s =>
                        s.Name.Equals(key, StringComparison.OrdinalIgnoreCase) ||
                        s.Symbol.Equals(key, StringComparison.OrdinalIgnoreCase) ||
                        BaseOf(s).Equals(key, StringComparison.OrdinalIgnoreCase))
                    .ToList();
        }

        private static string BaseOf(Subscription s) =>
            s.Exchange == Exchange.Upbit ? s.Symbol.Replace("KRW-", "")
            : s.Symbol.EndsWith("USDT") ? s.Symbol[..^4]
            : s.Symbol;
    }
}