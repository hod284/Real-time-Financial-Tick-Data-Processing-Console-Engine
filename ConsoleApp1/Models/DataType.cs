namespace ConsoleApp1.Models
{
    // ───────── 시장 이벤트 (거래소 → 파서 → 여기) ─────────

    // 공통 부모: 모든 시장 데이터가 공통으로 가진 정보
    public abstract record MarketEvent(
        Exchange Exchange,      // Upbit, Binance
        string Symbol,          // "KRW-BTC", "BTCUSDT"
        long ExchTimeMs,        // 거래소에서 발생한 시각
        long RecvTimeMs);       // 내가 받은 시각

    // 체결
    public sealed record TradeEvent(
        Exchange Exchange, string Symbol, long ExchTimeMs, long RecvTimeMs,
        decimal Price, decimal Qty, Side Side, long TradeId)
        : MarketEvent(Exchange, Symbol, ExchTimeMs, RecvTimeMs);

    // 호가
    public sealed record OrderBookEvent(
        Exchange Exchange, string Symbol, long ExchTimeMs, long RecvTimeMs,
        Level[] Bids, Level[] Asks)
        : MarketEvent(Exchange, Symbol, ExchTimeMs, RecvTimeMs);

    // 캔들
    public sealed record CandleEvent(
        Exchange Exchange, string Symbol, long ExchTimeMs, long RecvTimeMs,
        string Interval, long OpenTimeMs,
        decimal Open, decimal High, decimal Low, decimal Close, decimal Volume, bool IsClosed)
        : MarketEvent(Exchange, Symbol, ExchTimeMs, RecvTimeMs);

    // 티커
    public sealed record TickerEvent(
        Exchange Exchange, string Symbol, long ExchTimeMs, long RecvTimeMs,
        decimal LastPrice, double ChangeRate24h, decimal Volume24h, decimal High24h, decimal Low24h)
        : MarketEvent(Exchange, Symbol, ExchTimeMs, RecvTimeMs);

    public readonly record struct Level(decimal Price, decimal Qty);   // 호가 한 단계
    public enum Side { Buy, Sell, None }
    public enum Exchange { Upbit, Binance, None }

    // ───────── 분석 결과 ─────────

    public sealed record Alert(
        Exchange Exchange, string Symbol, string Rule, string Message,
        double Value, double Threshold, long TimeMs);

    // SymbolState(분석 수첩)의 복사본 → 웹·텔레그램은 이것만 읽음
    public sealed record AnalysisSnapshot(
        decimal LastPrice,
        decimal WindowLow,              // 최근 60초 최저가
        decimal WindowHigh,             // 최근 60초 최고가
        double RiseFromLowPct,          // 저점 대비 % (급등 기준과 비교)
        double FallFromHighPct,         // 고점 대비 % (급락 기준과 비교)
        decimal CurrentMinuteVolume,    // 이번 1분 거래량
        decimal AverageMinuteVolume,    // 최근 N분 평균
        double VolumeRatio,             // 이번 1분 / 평균 (거래량 급증 기준과 비교)
        int CompletedMinutes,           // 평균에 쓰인 분 수
        long TimeMs);

    // 종목 하나의 "지금 상태" 묶음 (MarketStore가 보관)
    public sealed record SymbolSnapshot(
        TradeEvent? Trade,
        OrderBookEvent? OrderBook,
        CandleEvent? Candle,
        TickerEvent? Ticker,
        AnalysisSnapshot? Analysis)
    {
        public static readonly SymbolSnapshot Empty = new(null, null, null, null, null);
    }

    // ───────── 구독 ─────────

    public sealed record Subscription(Exchange Exchange, string Symbol, string Name);

    // ───────── 설정 (AppSetting.json) ─────────

    public sealed record MarketOptions
    {
        public string DbPath { get; init; } = "market.db";
        public string[] BinanceSymbols { get; init; } = [];   // 첫 실행 기본 구독
        public string[] UpbitCodes { get; init; } = [];
        public DbWriterOptions DbWriter { get; init; } = new();
    }

    public sealed record DbWriterOptions
    {
        public int FlushIntervalMs { get; init; } = 1000;
        public int MaxBatchSize { get; init; } = 500;
    }

    public sealed record TelegramOptions
    {
        public string BotToken { get; init; } = "";
        public long ChatId { get; init; }
    }
}