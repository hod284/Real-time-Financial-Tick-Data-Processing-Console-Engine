using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

namespace ConsoleApp1.Storage
{
    //  [디스패처] --Enqueue()--> [큐(Channel)] --1초마다 꺼냄--> [트랜잭션 1개로 INSERT] --> SQLite
    public sealed class DbWriter
    {
        // object 큐: 체결/캔들/티커/알림 등 타입이 섞여 들어옴
        private readonly Channel<object> _queue =
            Channel.CreateUnbounded<object>(new UnboundedChannelOptions { SingleReader = true });

        private readonly Database _db;
        private readonly TradeRepository _trades;
        private readonly CandleRepository _candles;
        private readonly TickerRepository _tickers;   // [선택] ticker_latest 안 쓰면 제거
        private readonly AlertRepository _alerts;
        private readonly ILogger<DbWriter> _logger;
        private readonly TimeSpan _interval;
        private readonly int _maxBatch;

    }
}