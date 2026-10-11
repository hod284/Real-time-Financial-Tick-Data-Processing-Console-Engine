using ConsoleApp1.Parsing;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace ConsoleApp1.Adoator
{
    public sealed class UpbiteFeed(Func<IReadOnlyList<string>> getCodes) : WebSocketFeedBase
    {
        protected override string Name => "upbit";
        protected override bool HasTargets => getCodes().Count > 0;
        protected override Uri Endpoint => new("wss://api.upbit.com/websocket/v1");

        protected override Task OnConnectedAsync(ClientWebSocket ws, CancellationToken ct)
        {
            var codes = getCodes();
            var json = JsonSerializer.Serialize(new object[]
            {
                new { ticket = Guid.NewGuid().ToString("N") },
                new { type = "trade",     codes },
                new { type = "orderbook", codes },
                new { type = "ticker",    codes },
                new { type = "candle.1m", codes },   // "candle"이 아니라 "candle.1m"
            });
            return ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ct);
        }

        protected override void OnMessage(ReadOnlySpan<byte> payload, long recvMs)
        {
            var reader = new Utf8JsonReader(payload);
            using var doc = JsonDocument.ParseValue(ref reader);
            if (doc.RootElement.TryGetProperty("type", out var t))
            {
                switch (t.GetString())
                {
                    case "trade":     Emite(Parser.Parsetrade(Models.Exchange.Upbit, doc, recvMs)); break;
                    case "orderbook": Emite(Parser.orderBookEvent(Models.Exchange.Upbit, doc, recvMs)); break;
                    case "candle.1m": Emite(Parser.candleEvent(Models.Exchange.Upbit, doc, recvMs)); break;
                    case "ticker":    Emite(Parser.tickerEvent(Models.Exchange.Upbit, doc, recvMs)); break;
                }
            }
        }
    }
}