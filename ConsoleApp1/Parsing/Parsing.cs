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
                decimal trade_price = 0;
                decimal trade_volume = 0;
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
            string symbol = string.Empty;
            if(re.RootElement.TryGetProperty("stream", out var sta))
            {
                    var stream = sta.GetString()!;
                     symbol = stream[..stream.IndexOf('@')].ToUpperInvariant();   // "BTCUSDT"
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
            using var re = reader(message);
            Exchange exchange = Exchange.None;
            string symbol = string.Empty;
            long exchangetime = 0;
            string interval = string.Empty;
            long opentimeMs = 0;
            decimal open = 0;
            decimal high = 0;
            decimal low = 0;
            decimal close = 0;
            decimal volume = 0;
            bool isclosed = true;
            if (Exchange.Binance == ex)
            {
                exchange = Exchange.Binance;
                if (re.RootElement.TryGetProperty("data", out var d))
                {
                    symbol = d.GetProperty("s").GetString()!;
                    exchangetime  =d.GetProperty("E").GetInt64();
                    if (d.TryGetProperty("k", out var k))
                    {
                        interval = k.GetProperty("i").GetString()!;   // "1m"
                        opentimeMs = k.GetProperty("t").GetInt64();
                        open = k.GetProperty("o").GetDecimal();
                        high = k.GetProperty("h").GetDecimal();
                        low = k.GetProperty("l").GetDecimal();
                        close = k.GetProperty("c").GetDecimal();
                        volume = k.GetProperty("v").GetDecimal();
                        isclosed = k.GetProperty("x").GetBoolean();     // 바이낸스는 마감 플래그가 있음
                    }
                }
            }
            else
            {
                exchange = Exchange.Upbit;
                var ds = re.RootElement;
                if (ds.TryGetProperty("code", out var s))
                    symbol = s.GetString()!;
                if (ds.TryGetProperty("timestamp", out var time))
                    exchangetime = time.GetInt64();
                if (ds.TryGetProperty("candle_date_time_utc", out var optime))
                    opentimeMs = DateTimeOffset.Parse(optime.GetString() + "Z", CultureInfo.InvariantCulture).ToUnixTimeMilliseconds();
                if (ds.TryGetProperty("opening_price", out var opprice))
                    open = opprice.GetDecimal();
                if (ds.TryGetProperty("high_price", out var hp))
                    high = hp.GetDecimal();
                if (ds.TryGetProperty("low_price", out var lp))
                    low = lp.GetDecimal();
                if (ds.TryGetProperty("trade_pric", out var tp))
                    close = tp.GetDecimal();
                if (ds.TryGetProperty("candle_acc_trade_volume", out var vo))
                    volume = vo.GetDecimal();
                interval = "1m";
                isclosed = false;
            }
            return   new CandleEvent(
                      Exchange: exchange,
                      Symbol: symbol,
                      ExchTimeMs: exchangetime,
                      RecvTimeMs: recvMs,
                       Interval: interval,   // "1m"
                       OpenTimeMs: opentimeMs,
                       Open: open,
                       High: high,
                       Low: low,
                       Close: close,
                       Volume: volume,
                       IsClosed: isclosed);     // 바이낸스는 마감 플래그가 있음
        }
        
        public static TickerEvent tickerEvent(Exchange ex, ReadOnlySpan<byte> message, long recvMs)
        {
            using var re = reader(message);
            Exchange exchange = Exchange.None;
            string symbol = string.Empty;
            long exchangtime = 0;
            decimal lastprice = 0;
            double changerate24 = 0;
            decimal volum24 = 0;
            decimal high24 = 0;
            decimal low24 = 0;
            if (Exchange.Binance == ex)
            {
                exchange = Exchange.Binance;
                if (re.RootElement.TryGetProperty("data", out var d))
                {
                     symbol = d.GetProperty("s").GetString()!;
                    exchangtime = d.GetProperty("E").GetInt64();
                    lastprice = d.GetProperty("c").GetDecimal();
                    changerate24 = (double)d.GetProperty("P").GetDecimal()/100;
                    volum24 = d.GetProperty("v").GetDecimal();
                     high24 = d.GetProperty("h").GetDecimal();
                    low24 = d.GetProperty("l").GetDecimal();
                }
            }
            else
            {
                exchange = Exchange.Upbit;
                var r = re.RootElement;
                if (r.TryGetProperty("code", out var sy))
                    symbol = sy.GetString()!;
                if (r.TryGetProperty("timestamp", out var os))
                         exchangtime = os.GetInt64();
                if (r.TryGetProperty("trade_price",out var tp ))
                    lastprice =tp.GetDecimal();
                if (r.TryGetProperty("signed_change_rate", out var scr))// 이미 비율(0.05 = 5%))   
                    changerate24 = scr.GetDouble();
                if(r.TryGetProperty("acc_trade_volume_24h", out var atv))
                    volum24 = atv.GetDecimal();
                if (r.TryGetProperty("high_price", out var h))   // 주의: 당일(KST 09시 기준) 고가
                    high24 = h.GetDecimal();
                if (r.TryGetProperty("low_price", out var lo))
                    low24 = lo.GetDecimal();
            }
            return  new TickerEvent(
                  Exchange:exchange,
                  Symbol: symbol,
                  ExchTimeMs: exchangtime,
                  RecvTimeMs:recvMs,
                  LastPrice: lastprice,
                  ChangeRate24h: changerate24,
                  Volume24h: volum24,
                  High24h:high24,
                  Low24h:low24
                );
        }

    }
   
   
}
