using ConsoleApp1.Models;

namespace ConsoleApp1.Analysis
{
    // 거래량 급증 감지
    //  진행 중인 1분 거래량이 최근 20분 평균의 3배를 넘는 순간 알림 (1분 끝날 때까지 안 기다림)
    //  거래량 단위가 종목마다 달라도 자기 평균과 비교하니까 상관없음
    public sealed class VolumeSpikeRule : IAlertRule
    {
        public const int LookbackMinutes = 20;    // AnalysisEngine에서도 씀
        private const int MinHistoryMinutes = 5;  // 최소 5분은 쌓여야 판단
        private const double Multiplier = 3.0;
        private const int CooldownSeconds = 300;

        public string Name => "VolumeSpike";

        public Alert? Evaluate(TradeEvent trade, SymbolState state)
        {
            if (state.CompletedMinutes < MinHistoryMinutes)   // 켜자마자는 비교 대상이 없음
                return null;
            if (state.IsCoolingDown(Name, state.LastTimeMs, CooldownSeconds))
                return null;

            decimal avg = state.AverageMinuteVolume;
            if (avg <= 0)
                return null;

            double ratio = (double)(state.CurrentMinuteVolume / avg);
            if (ratio < Multiplier)
                return null;

            state.MarkAlerted(Name, state.LastTimeMs);
            string message = $"📊 [{state.Exchange}] {state.Symbol} 거래량 급증 {ratio:F1}배 " +
                             $"(이번 1분 {state.CurrentMinuteVolume} / 최근 {state.CompletedMinutes}분 평균 {avg:F4}), 현재가 {state.LastPrice}";
            return new Alert(state.Exchange, state.Symbol, Name, message, ratio, Multiplier, state.LastTimeMs);
        }
    }
}