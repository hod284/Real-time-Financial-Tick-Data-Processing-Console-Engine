using ConsoleApp1.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Diagnostics.SymbolStore;
using System.Globalization;
using System.Text;
using System.Text.Json;
namespace ConsoleApp1.Parsing
{
    public static class Parser
    {
        private static JsonDocument reader(ReadOnlySpan<byte> message)
        {
            var recorde = new Utf8JsonReader(message);
           return   JsonDocument.ParseValue(ref recorde);
           
        }
        public static TradeEvent Parsetrade(Exchange ex, ReadOnlySpan<byte> message, long recvMs)
        {
            using var re = reader(message);
            TradeEvent trade;
            if (Exchange.Binance == ex)
            {
                if (re.RootElement.TryGetProperty("data", out var va))
                {
                    trade = new TradeEvent(                                 // ★ 여기서 틀에 값을 채워 객체 생성
                              Exchange: ex,
                              Symbol: va.GetProperty("s").GetString()!,                 // "BTCUSDT"
                              ExchTimeMs: va.GetProperty("T").GetInt64(),                   // 체결 시각
                              RecvTimeMs: recvMs,                                           // 받은시각
                              Price: decimal.Parse(va.GetProperty("p").GetString()!, CultureInfo.InvariantCulture),  // "65432.10" → 65432.10
                              Qty: decimal.Parse(va.GetProperty("q").GetString()!, CultureInfo.InvariantCulture),  // "0.012"    → 0.012
                              Side: va.GetProperty("m").GetBoolean() ? Side.Sell : Side.Buy);
                }
                else
                {
                    trade = new TradeEvent(                                 // ★ 여기서 틀에 값을 채워 객체 생성
                              Exchange: ex,
                              Symbol: "",                 // "BTCUSDT"
                              ExchTimeMs: 0,                   // 체결 시각
                              RecvTimeMs: recvMs,                                           // 받은시각
                              Price: 0,  // "65432.10" → 65432.10
                              Qty: 0,  // "0.012"    → 0.012
                              Side: Side.None);
                }
            }
            else
            {
                var va = re.RootElement;
                string symbol = string.Empty;
                decimal trade_price = decimal.MinusOne;
                decimal trade_volume = decimal.MinusOne;
                Side side = Side.None;
                if (va.TryGetProperty("code", out var sy))
                    symbol = sy.GetString()!;
                if (va.TryGetProperty("trade_price", out var tp))
                    trade_price = tp.GetDecimal();
                if (va.TryGetProperty("trade_volume", out var tv))
                    trade_volume = tv.GetDecimal()!;
                if (va.TryGetProperty("ask_bid", out var ab))
                    side = ab.GetString() == "BID" ? Side.Buy : Side.Sell;
                trade = new TradeEvent(
                Exchange: ex,
                Symbol: symbol,          // "KRW-BTC"
                ExchTimeMs: va.GetProperty("trade_timestamp").GetInt64(), // 체결 시각
                RecvTimeMs: recvMs,                                       // 받은시각
                Price: trade_price,   // 숫자로 옴 → 바로 읽음
                Qty: trade_volume,
                Side: side);
            }
            return trade;
        }

        public static OrderBookEvent orderBookEvent(Exchange ex, ReadOnlySpan<byte> message, long recvMs)
        {
            using var re = reader(message);
            if(re.TryGetProperty("stream", out var st))
            {
                    var stream = st.GetString()!;
                    var symbol = stream[..stream.IndexOf('@')].ToUpperInvariant();   // "BTCUSDT"
            }
            OrderBookEvent orderBook;
            if (Exchange.Binance == ex)
            {
                if (re.RootElement.TryGetProperty("data", out var s))
                {
                    
                    var levels = new Level[s.GetProperty("bids").GetArrayLength()];
                    int i = 0;
                    foreach (var lv in s.GetProperty("bids").EnumerateArray())
                    {
                        levels[i++] = new Level(decimal.TryParse(lv[0].GetString(), out var price) ? price : 0, decimal.TryParse(lv[1].GetString()!, out var qty) ? qty : -1);
                    }
                    var asks = new Level[s.GetProperty("asks").GetArrayLength()];
                    i = 0;
                    foreach (var lv in s.GetProperty("asks").EnumerateArray())
                    {
                        asks[i++] = new Level(decimal.TryParse(lv[0].GetString(), out var price) ? price : 0, decimal.TryParse(lv[1].GetString()!, out var qty) ? qty : -1);
                    }
                    orderBook = new OrderBookEvent(
                        Exchange: ex,
                        Symbol: symbol,
                        ExchTimeMs: recvMs,   // 교환 시간을 따로 안주어서 받는시간만 줌
                        RecvTimeMs: recvMs,   //
                        Bids: levels,
                        Asks: asks
                        );
                }
                else
                {
                    orderBook = new OrderBookEvent(
                          Exchange: ex,
                          Symbol: "",
                          ExchTimeMs: recvMs,   // 교환 시간을 따로 안주어서 받는시간만 줌
                          RecvTimeMs: recvMs,   //
                          Bids: null,
                          Asks: null
                          );
                }
            }
            else
            {
                int i = 0;
               string sy = string.Empty;
                long time = 0;
                if (re.RootElement.TryGetProperty("orderbook_units", out var s))
                {
                    var units = s;
                    int n = units.GetArrayLength();
                    var bids = new Level[n];
                    var asks = new Level[n];
                    foreach (var u in units.EnumerateArray())
                    {
                        bids[i] = new Level(u.GetProperty("bid_price").GetDecimal(), u.GetProperty("bid_size").GetDecimal());
                        asks[i] = new Level(u.GetProperty("ask_price").GetDecimal(), u.GetProperty("ask_size").GetDecimal());
                        i++;
                    }
                    if (s.TryGetProperty("code", out var st))
                        sy = st.GetString()!;
                    if (s.TryGetProperty("timestamp", out var ti))
                        time = ti.GetInt64();
                    orderBook = new OrderBookEvent(
                        Exchange: ex,
                        Symbol: sy,
                        ExchTimeMs: time,
                        RecvTimeMs: recvMs,
                         Bids: bids,
                          Asks: asks);
                }
                else
                {
                    orderBook = new OrderBookEvent(
                           Exchange: ex,
                           Symbol: sy,
                           ExchTimeMs: time,
                           RecvTimeMs: recvMs,
                            Bids: null,
                             Asks: null);
                }

            }
            return orderBook;
        }
        
        public static CandleEvent candleEvent(Exchange ex, ReadOnlySpan<byte> message, long recvMs)
        {

        }
        
        public static TickerEvent tickerEvent(Exchange ex, ReadOnlySpan<byte> message, long recvMs)
        {

        }

    }
   
   
}
