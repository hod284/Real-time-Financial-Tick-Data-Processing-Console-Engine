using System;
using System.Collections.Generic;
using System.Text;

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
            throw new NotImplementedException();
        }
    }
}
