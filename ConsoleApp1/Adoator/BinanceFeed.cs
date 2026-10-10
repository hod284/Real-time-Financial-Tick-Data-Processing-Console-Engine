using ConsoleApp1.Parsing;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace ConsoleApp1.Adoator
{
    public sealed class BinanceFeed(IReadOnlyList<string> symbols) : WebSocketFeedBase
    {
        protected override string Name => "binance";

        // 바이낸스는 주소에 구독할 스트림을 넣음 → OnConnectedAsync 필요 없음
        protected override Uri Endpoint => new("wss://stream.binance.com:9443/stream?streams=" +
            string.Join('/', symbols.SelectMany(s => new[]
            {
            $"{s}@aggTrade", $"{s}@depth10@100ms", $"{s}@ticker", $"{s}@kline_1m"
            })));

        protected override void OnMessage(ReadOnlySpan<byte> payload, long recvMs)
        {
            var reader = new Utf8JsonReader(payload);
            using var doc = JsonDocument.ParseValue(ref reader);
            if (doc.RootElement.TryGetProperty("stream", out var s)) 
            {
                var stream = s.GetString()!; 
                 if(stream.EndsWith("@aggTrade")) 
                     Emite(Parser.Parsetrade(Models.Exchange.Binance, doc, recvMs));
                 else if(stream.Contains("@depth"))    
                     Emite(Parser.orderBookEvent(Models.Exchange.Binance, doc, recvMs));
               else  if (stream.Contains("@kline_"))
                    Emite(Parser.candleEvent(Models.Exchange.Binance, doc, recvMs));
                else if (stream.EndsWith("@ticker"))
                    Emite(Parser.tickerEvent(Models.Exchange.Binance, doc, recvMs));
            }
        }
    
    }
}
