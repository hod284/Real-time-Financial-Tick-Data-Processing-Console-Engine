using ConsoleApp1.Models;
using System.Collections.Concurrent;

namespace ConsoleApp1.Store
{
    // 종목별 "최신 값" 보관소 (메모리)
    //  쓰기: 디스패처만 (Update, UpdateAnalysis)
    //  읽기: 웹·텔레그램 (Get)
    // 값이 전부 record(불변)라서 "통째로 바꿔 끼우기"만 함
    // → 읽는 쪽은 언제 읽어도 완성된 값을 봄 (반쯤 바뀐 값을 볼 일이 없음)
    public sealed class MarketStore
    {
        private readonly ConcurrentDictionary<(Exchange, string), SymbolSnapshot> _map = new();

        public void Update(MarketEvent e)
        {
            var key = (e.Exchange, e.Symbol);
            var cur = Get(e.Exchange, e.Symbol);
            _map[key] = e switch
            {
                TradeEvent t     => cur with { Trade = t },       // with: 나머지는 그대로 두고 이것만 바꾼 새 복사본
                OrderBookEvent b => cur with { OrderBook = b },
                CandleEvent c    => cur with { Candle = c },
                TickerEvent k    => cur with { Ticker = k },
                _                => cur,
            };
        }

        public void UpdateAnalysis(Exchange ex, string symbol, AnalysisSnapshot analysis)
        {
            _map[(ex, symbol)] = Get(ex, symbol) with { Analysis = analysis };
        }

        public SymbolSnapshot Get(Exchange ex, string symbol)
            => _map.TryGetValue((ex, symbol), out var s) ? s : SymbolSnapshot.Empty;
    }
}