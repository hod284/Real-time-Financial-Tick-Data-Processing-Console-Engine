using ConsoleApp1.Models;
using System.Text.Json;

namespace ConsoleApp1.singleton
{
    // 업비트/바이낸스 종목 "한글이름 ↔ 코드" 변환표
    //  - 시작할 때 거래소 REST에서 한 번 받아옴 (LoadAsync)
    //  - 바이낸스는 한글명이 없어서 업비트 한글명을 빌려옴 (BTCUSDT ← KRW-BTC의 "비트코인")
    //  - 시작 스레드와 웹 요청 스레드가 같이 쓸 수 있어서 lock
    public sealed class SymbolDirectory
    {
        private readonly object _lock = new();
        private readonly Dictionary<(Exchange, string), string> _nameToCode = new(new KeyComparer());
        private readonly Dictionary<(Exchange, string), string> _codeToName = new(new KeyComparer());

        public void Add(Exchange ex, string name, string code)
        {
            lock (_lock)
            {
                _nameToCode[(ex, name)] = code;
                _codeToName[(ex, code)] = name;
            }
        }

        public string? FindCode(Exchange ex, string name)
        {
            lock (_lock) return _nameToCode.TryGetValue((ex, name), out var code) ? code : null;
        }

        public string? FindName(Exchange ex, string code)
        {
            lock (_lock) return _codeToName.TryGetValue((ex, code), out var name) ? name : null;
        }

        public bool HasCode(Exchange ex, string code)
        {
            lock (_lock) return _codeToName.ContainsKey((ex, code));
        }

        private bool IsLoaded(Exchange ex)
        {
            lock (_lock) return _codeToName.Keys.Any(k => k.Item1 == ex);
        }

        // 입력 → 거래소 코드
        //  업비트: "비트코인" / "BTC" / "KRW-BTC"  → "KRW-BTC"
        //  바이낸스: "비트코인" / "BTC" / "BTCUSDT" → "BTCUSDT"
        //  없는 종목이면 null
        public string? Resolve(Exchange ex, string input)
        {
            input = input.Trim();
            if (FindCode(ex, input) is { } byName)
                return byName;

            string code = ex == Exchange.Upbit
                ? (input.StartsWith("KRW-", StringComparison.OrdinalIgnoreCase) ? input : "KRW-" + input).ToUpperInvariant()
                : (input.EndsWith("USDT", StringComparison.OrdinalIgnoreCase) ? input : input + "USDT").ToUpperInvariant();

            if (!IsLoaded(ex))
                return code;              // 목록을 못 받아왔으면 일단 믿고 진행
            return HasCode(ex, code) ? code : null;
        }

        public async Task LoadAsync(CancellationToken ct)
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            try
            {
                // 업비트: [{ "market": "KRW-BTC", "korean_name": "비트코인", ... }]
                using var upbit = JsonDocument.Parse(await http.GetStringAsync("https://api.upbit.com/v1/market/all", ct));
                foreach (var m in upbit.RootElement.EnumerateArray())
                {
                    var code = m.GetProperty("market").GetString()!;
                    if (code.StartsWith("KRW-"))
                        Add(Exchange.Upbit, m.GetProperty("korean_name").GetString()!, code);
                }

                // 바이낸스: [{ "symbol": "BTCUSDT", "price": "..." }] → USDT 마켓만
                using var binance = JsonDocument.Parse(await http.GetStringAsync("https://api.binance.com/api/v3/ticker/price", ct));
                foreach (var t in binance.RootElement.EnumerateArray())
                {
                    var symbol = t.GetProperty("symbol").GetString()!;
                    if (!symbol.EndsWith("USDT")) continue;
                    var baseAsset = symbol[..^4];
                    var name = FindName(Exchange.Upbit, "KRW-" + baseAsset) ?? baseAsset;
                    Add(Exchange.Binance, name, symbol);
                }
                Console.WriteLine("종목 목록 로드 완료");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"종목 목록 로드 실패 (코드로만 구독 가능): {ex.Message}");
            }
        }

        // 대소문자 무시 비교 (btcusdt == BTCUSDT)
        private sealed class KeyComparer : IEqualityComparer<(Exchange, string)>
        {
            public bool Equals((Exchange, string) a, (Exchange, string) b)
                => a.Item1 == b.Item1 && string.Equals(a.Item2, b.Item2, StringComparison.OrdinalIgnoreCase);

            public int GetHashCode((Exchange, string) k)
                => HashCode.Combine(k.Item1, StringComparer.OrdinalIgnoreCase.GetHashCode(k.Item2));
        }
    }
}
