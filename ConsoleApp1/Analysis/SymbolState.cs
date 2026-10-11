using ConsoleApp1.Models;

namespace ConsoleApp1.Analysis
{
    // 종목 하나(거래소+심볼)의 "최근 상태"를 메모리에 보관
    //  1) 최근 N초 최저가/최고가 → 급등락 판단용
    //  2) 1분 거래량 (진행 중인 1분 + 지난 N분) → 거래량 급증 판단용
    //  3) 룰별 마지막 알림 시각 → 쿨다운
    // 시간은 전부 거래소 시각(ExchTimeMs) 기준
    // lock 없음 → 디스패처 한 곳에서만 호출해야 함
    public sealed class SymbolState
    {
        private const long MinuteMs = 60_000;

        public Exchange Exchange { get; }
        public string Symbol { get; }
        public long LastTimeMs { get; private set; }
        public decimal LastPrice { get; private set; }

        // ── 1) 최근 N초 최저가/최고가 (단조 덱) ──
        private readonly long _priceWindowMs;
        private readonly LinkedList<(long TimeMs, decimal Price)> _minDeque = new();
        private readonly LinkedList<(long TimeMs, decimal Price)> _maxDeque = new();
        public decimal WindowLow => _minDeque.First!.Value.Price;    // 맨 앞 = 최저가
        public decimal WindowHigh => _maxDeque.First!.Value.Price;   // 맨 앞 = 최고가

        // ── 2) 1분 거래량 ──
        private readonly int _volumeLookback;
        private readonly Queue<decimal> _minuteVolumes = new();   // 지난 N분 거래량 (오래된 것부터)
        private long _currentMinuteStartMs;
        private bool _firstMinuteDone;
        public decimal CurrentMinuteVolume { get; private set; }   // 지금 진행 중인 1분
        public int CompletedMinutes => _minuteVolumes.Count;
        public decimal AverageMinuteVolume => _minuteVolumes.Count == 0 ? 0 : _minuteVolumes.Average();

        // ── 3) 쿨다운 ──
        private readonly Dictionary<string, long> _lastAlertMs = new();

        public SymbolState(Exchange exchange, string symbol, int priceWindowSeconds, int volumeLookbackMinutes)
        {
            Exchange = exchange;
            Symbol = symbol;
            _priceWindowMs = priceWindowSeconds * 1000L;
            _volumeLookback = volumeLookbackMinutes;
        }

        public void OnTrade(TradeEvent t)
        {
            long now = Math.Max(t.ExchTimeMs, LastTimeMs);   // 순서가 살짝 뒤바뀌어 와도 시간이 뒤로 안 가게
            LastTimeMs = now;
            LastPrice = t.Price;
            UpdatePriceWindow(now, t.Price);
            UpdateMinuteVolume(now, t.Qty);
        }

        private void UpdatePriceWindow(long now, decimal price)
        {
            // 새 가격보다 비싼 건 앞으로 "최저가"가 될 일이 없으니 뒤에서 버림
            while (_minDeque.Count > 0 && _minDeque.Last!.Value.Price >= price) _minDeque.RemoveLast();
            _minDeque.AddLast((now, price));

            // 새 가격보다 싼 건 앞으로 "최고가"가 될 일이 없으니 뒤에서 버림
            while (_maxDeque.Count > 0 && _maxDeque.Last!.Value.Price <= price) _maxDeque.RemoveLast();
            _maxDeque.AddLast((now, price));

            // 윈도우(60초)보다 오래된 건 앞에서 버림
            long cutoff = now - _priceWindowMs;
            while (_minDeque.First!.Value.TimeMs < cutoff) _minDeque.RemoveFirst();
            while (_maxDeque.First!.Value.TimeMs < cutoff) _maxDeque.RemoveFirst();
            // 방금 넣은 값은 항상 cutoff보다 새로우니 덱이 비는 일은 없음
        }

        private void UpdateMinuteVolume(long now, decimal qty)
        {
            long minuteStart = now / MinuteMs * MinuteMs;   // 10:03:27 → 10:03:00

            if (_currentMinuteStartMs == 0)
            {
                _currentMinuteStartMs = minuteStart;          // 첫 체결
            }
            else if (minuteStart > _currentMinuteStartMs)     // 분이 넘어감
            {
                // 첫 1분은 프로그램이 중간에 켜져서 덜 셌으니 버림 (평균이 낮아져 오탐 나는 것 방지)
                if (_firstMinuteDone)
                    _minuteVolumes.Enqueue(CurrentMinuteVolume);
                _firstMinuteDone = true;

                // 체결이 아예 없던 분은 거래량 0으로 채움
                long emptyMinutes = (minuteStart - _currentMinuteStartMs) / MinuteMs - 1;
                for (long i = 0; i < Math.Min(emptyMinutes, _volumeLookback); i++)
                    _minuteVolumes.Enqueue(0m);

                while (_minuteVolumes.Count > _volumeLookback)   // N분치만 유지
                    _minuteVolumes.Dequeue();

                _currentMinuteStartMs = minuteStart;
                CurrentMinuteVolume = 0;
            }
            CurrentMinuteVolume += qty;
        }

        public bool IsCoolingDown(string rule, long nowMs, int cooldownSeconds)
        {
           return _lastAlertMs.TryGetValue(rule, out var last) && nowMs - last < cooldownSeconds * 1000L;
        }
        public void MarkAlerted(string rule, long nowMs) 
        {
            _lastAlertMs[rule] = nowMs;
        }
        // 수첩 복사본 만들기 → MarketStore에 넣어서 웹·텔레그램이 읽게 함
        // (디스패처가 OnTrade 직후에 부르므로 덱이 비어 있을 일이 없음)
        public AnalysisSnapshot ToSnapshot()
        {
            decimal low = WindowLow, high = WindowHigh, avg = AverageMinuteVolume;
            return new AnalysisSnapshot(
                LastPrice,
                low,
                high,
                low > 0 ? (double)((LastPrice - low) / low * 100m) : 0,
                high > 0 ? (double)((LastPrice - high) / high * 100m) : 0,
                CurrentMinuteVolume,
                avg,
                avg > 0 ? (double)(CurrentMinuteVolume / avg) : 0,
                CompletedMinutes,
                LastTimeMs);
        }
    }
}