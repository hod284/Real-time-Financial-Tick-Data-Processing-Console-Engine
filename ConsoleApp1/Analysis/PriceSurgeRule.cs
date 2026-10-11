using ConsoleApp1.Models;

namespace ConsoleApp1.Analysis
{
    // 급등/급락 감지
    //  - 급등: 최근 60초 최저가 대비 현재가 +1% 이상
    //  - 급락: 최근 60초 최고가 대비 현재가 -1% 이하
    // "60초 전 가격"이 아니라 "60초 안의 최저/최고"와 비교 → V자 반등도 잡음
    public sealed class PriceSurgeRule : IAlertRule
    {
        public const int WindowSeconds = 60;     // AnalysisEngine에서도 씀
        private const double ThresholdPercent = 1.0;
        private const int CooldownSeconds = 300; // 같은 종목 5분간 재알림 X

        public string Name => "PriceSurge";

        public Alert? Evaluate(TradeEvent trade, SymbolState state)
        {
            if (state.IsCoolingDown(Name, state.LastTimeMs, CooldownSeconds))
                return null;

            decimal price = state.LastPrice;
            decimal low = state.WindowLow;
            decimal high = state.WindowHigh;
            if (low <= 0 || high <= 0)
                return null;

            double risePct = (double)((price - low) / low * 100m);    // 저점 대비 상승률 (0 이상)
            double fallPct = (double)((price - high) / high * 100m);  // 고점 대비 하락률 (0 이하)

            string message;
            double value;
            if (risePct >= ThresholdPercent && risePct >= -fallPct)
            {
                value = risePct;
                message = $"🚀 [{state.Exchange}] {state.Symbol} {WindowSeconds}초 내 +{risePct:F2}% 급등 (저점 {low} → 현재 {price})";
            }
            else if (-fallPct >= ThresholdPercent)
            {
                value = fallPct;
                message = $"📉 [{state.Exchange}] {state.Symbol} {WindowSeconds}초 내 {fallPct:F2}% 급락 (고점 {high} → 현재 {price})";
            }
            else
            {
                return null;   // 조건 안 맞음
            }

            state.MarkAlerted(Name, state.LastTimeMs);   // 쿨다운 시작
            return new Alert(state.Exchange, state.Symbol, Name, message, value, ThresholdPercent, state.LastTimeMs);
        }
    }
}