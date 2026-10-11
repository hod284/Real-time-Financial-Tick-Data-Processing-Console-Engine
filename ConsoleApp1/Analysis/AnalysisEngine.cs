using ConsoleApp1.Models;
using ConsoleApp1.Store;

namespace ConsoleApp1.Analysis
{
    // 체결이 오면
    //   ① 종목별 SymbolState(수첩)를 찾고 (없으면 생성)
    //   ② 수첩 갱신 → 복사본을 MarketStore에 저장 (화면 표시용)
    //   ③ 등록된 모든 룰에 물어봐서 나온 알림을 돌려줌
    // 디스패처에서만 호출 (SymbolState에 lock이 없어서)
    public sealed class AnalysisEngine
    {
        private readonly IReadOnlyList<IAlertRule> _rules;
        private readonly MarketStore _store;
        private readonly Dictionary<(Exchange, string), SymbolState> _states = new();

        public AnalysisEngine(IEnumerable<IAlertRule> rules, MarketStore store)
        {
            _rules = rules.ToList();
            _store = store;
        }

        public IReadOnlyList<Alert> Process(MarketEvent e)
        {
            if (e is not TradeEvent trade || trade.Price <= 0 || trade.Symbol.Length == 0)
                return Array.Empty<Alert>();

            // ① 종목별 수첩 찾기
            var key = (trade.Exchange, trade.Symbol);
            if (!_states.TryGetValue(key, out var state))
            {
                state = new SymbolState(trade.Exchange, trade.Symbol,
                    PriceSurgeRule.WindowSeconds, VolumeSpikeRule.LookbackMinutes);
                _states[key] = state;
            }

            // ② 수첩 갱신 + 복사본 저장
            state.OnTrade(trade);
            _store.UpdateAnalysis(trade.Exchange, trade.Symbol, state.ToSnapshot());

            // ③ 룰마다 물어보기
            List<Alert>? alerts = null;
            foreach (var rule in _rules)
            {
                var alert = rule.Evaluate(trade, state);
                if (alert != null)
                    (alerts ??= new List<Alert>()).Add(alert);
            }
            return alerts ?? (IReadOnlyList<Alert>)Array.Empty<Alert>();
        }
    }
}