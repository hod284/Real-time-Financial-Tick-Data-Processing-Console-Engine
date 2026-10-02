using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ConsoleApp1.Parsing;
namespace ConsoleApp1.Adoator
{
    public sealed class UpbiteFeed(IReadOnlyList<string> codes) : WebSocketFeedBase
    {
        protected override string Name => "Upbite";

        protected override Uri Endpoint => new("wss://api.upbit.com/websocket/v1");

        protected override Task OnConnectedAsync(ClientWebSocket ws, CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(new object[] {
            new { ticket = Guid.NewGuid().ToString("N") },
            new { type = "trade",     codes },
            new { type = "orderbook", codes },
            new { type = "ticker",    codes },
            new { type = "candle",    codes },
             });
            return ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ct);
        }

        protected override void OnMessage(ReadOnlySpan<byte> payload, long recvMs)
        {
        

            Emite(Parser.Parsetrade(Models.Exchange.Upbit, payload, recvMs));
            Emite(Parser.orderBookEvent(Models.Exchange.Upbit, payload, recvMs));
        }
    }
}
