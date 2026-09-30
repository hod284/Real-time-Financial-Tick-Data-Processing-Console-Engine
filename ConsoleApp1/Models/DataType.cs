namespace ConsoleApp1.Models
{
    // 공통 부모: 모든 시장 데이터가 공통으로 가진 정보
    public abstract record MarketEvent(
        string Exchange,// "upbit", "binance"
          string Symbol, // "KRW-BTC", "BTCUSDT"
          long ExchTimeMs, // 거래소에서 발생한 시각
          long RecvTimeMs  // 내가 받은 시각
          );

    // 체결
    public sealed record TradeEvent(
        string Exchange, 
        string Symbol,
        long ExchTimeMs,
        long RecvTimeMs,
        decimal Price,
        decimal Qty, 
        Side Side) : MarketEvent(Exchange, Symbol, ExchTimeMs, RecvTimeMs);

    // 호가 (depth면 여러 단계, bookTicker면 1단계만 들어감)
    public sealed record OrderBookEvent(
        string Exchange,
        string Symbol, 
        long ExchTimeMs, 
        long RecvTimeMs,
        Level[] Bids,
        Level[] Asks) : MarketEvent(Exchange, Symbol, ExchTimeMs, RecvTimeMs);

    // 캔들 ← 새로 추가
    public sealed record CandleEvent(
        string Exchange, 
        string Symbol, 
        long ExchTimeMs,
        long RecvTimeMs,
        string Interval,
        long OpenTimeMs,
        decimal Open, 
        decimal High,
        decimal Low,
        decimal Close, 
        decimal Volume,
        bool IsClosed) : MarketEvent(Exchange, Symbol, ExchTimeMs, RecvTimeMs);

    // 티커
    public sealed record TickerEvent(
        string Exchange,
        string Symbol,
        long ExchTimeMs,
        long RecvTimeMs,
        decimal LastPrice, 
        double ChangeRate24h,
        decimal Volume24h, 
        decimal High24h,
        decimal Low24h)
        : MarketEvent(Exchange, Symbol, ExchTimeMs, RecvTimeMs);

    public readonly record struct Level(decimal Price, decimal Qty);   // 호가 한 단계
    public enum Side { Buy, Sell }
    // 비트 코인 종류 구분
    public enum Exchange { Upbit, Binance }
}
