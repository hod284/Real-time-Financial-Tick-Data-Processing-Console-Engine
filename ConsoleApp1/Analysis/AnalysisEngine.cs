using ConsoleApp1.Models;

namespace ConsoleApp1.Analysis
{
    // 체결이 오면
    //   ① 종목별 SymbolState를 찾고 (없으면 생성)
    //   ② 상태를 갱신하고
    //   ③ 등록된 모든 룰에 물어봐서 나온 알림을 돌려줌
    // 메모리 계산만 해서 빠름 → 디스패처에서 바로 호출해도 수신이 안 밀림
    public sealed class AnalysisEngine
    {
        private readonly IReadOnlyList<IAlertRule> _rules;
        private readonly Dictionary<(Exchange, string), SymbolState> _states = new();

        // DI가 등록된 IAlertRule 전부를 넣어줌
        public AnalysisEngine(IEnumerable<IAlertRule> rules)
        {
            _rules = rules.ToList();
        }

        public IReadOnlyList<Alert> Process(MarketEvent e)
        {
            // 지금 룰들은 체결만 봄 (호가/티커/캔들은 저장용)
            if (e is not TradeEvent trade || trade.Price <= 0 || trade.Symbol.Length == 0)
                return Array.Empty<Alert>();

            // ① 종목별 상태 찾기 (바이낸스 BTCUSDT와 업비트 KRW-BTC는 따로 관리)
            var key = (trade.Exchange, trade.Symbol);
            if (!_states.TryGetValue(key, out var state))
            {
                state = new SymbolState(trade.Exchange, trade.Symbol,
                    PriceSurgeRule.WindowSeconds, VolumeSpikeRule.LookbackMinutes);
                _states[key] = state;
            }

            // ② 상태 갱신
            state.OnTrade(trade);

            // ③ 룰마다 물어보기
            List<Alert>? alerts = null;
            foreach (var rule in _rules)
            {
                var alert = rule.Evaluate(trade, state);
                if (alert != null)
                    (alerts ??= new List<Alert>()).Add(alert);   // 알림 있을 때만 리스트 생성
            }
            return alerts ?? (IReadOnlyList<Alert>)Array.Empty<Alert>();
        }
    }
}