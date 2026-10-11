using ConsoleApp1.Parsing;
using System.Text.Json;

namespace ConsoleApp1.Adoator
{
    // getSymbols: "지금 구독 목록"을 꺼내는 함수 → 연결할 때마다 불러서 최신 목록 사용
    public sealed class BinanceFeed(Func<IReadOnlyList<string>> getSymbols) : WebSocketFeedBase
    {
        protected override string Name => "binance";
        protected override bool HasTargets => getSymbols().Count > 0;

        protected override Uri Endpoint => new("wss://stream.binance.com:9443/stream?streams=" +
            string.Join('/', getSymbols().SelectMany(s =>
            {
                var l = s.ToLowerInvariant();   // 스트림 이름은 소문자
                return new[] { $"{l}@aggTrade", $"{l}@depth10@100ms", $"{l}@ticker", $"{l}@kline_1m" };
            })));

        protected override void OnMessage(ReadOnlySpan<byte> payload, long recvMs)
        {
            var reader = new Utf8JsonReader(payload);
            using var doc = JsonDocument.ParseValue(ref reader);
            if (doc.RootElement.TryGetProperty("stream", out var s))
            {
                var stream = s.GetString()!;
                if (stream.EndsWith("@aggTrade"))
                    Emite(Parser.Parsetrade(Models.Exchange.Binance, doc, recvMs));
                else if (stream.Contains("@depth"))
                    Emite(Parser.orderBookEvent(Models.Exchange.Binance, doc, recvMs));
                else if (stream.Contains("@kline_"))
                    Emite(Parser.candleEvent(Models.Exchange.Binance, doc, recvMs));
                else if (stream.EndsWith("@ticker"))
                    Emite(Parser.tickerEvent(Models.Exchange.Binance, doc, recvMs));
            }
        }
    }
}