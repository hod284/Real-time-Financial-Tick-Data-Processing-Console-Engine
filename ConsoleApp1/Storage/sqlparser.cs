using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleApp1.Storage
{
    public sealed class CandleRow
    {
        public string Exchange { get; set; } = "";
        public string Symbol { get; set; } = "";
        public string Interval { get; set; } = "";
        public long OpenTimeMs { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }
        public long IsClosed { get; set; }   // 0 / 1 (SQLite엔 bool 없음)
    }
    public sealed class AlertRow
    {
        public long Id { get; set; }
        public string Exchange { get; set; } = "";
        public string Symbol { get; set; } = "";
        public string Rule { get; set; } = "";
        public string Message { get; set; } = "";
        public double Value { get; set; }
        public double Threshold { get; set; }
        public long TimeMs { get; set; }
    }
    public sealed class TickerRow
    {
        public string Exchange { get; set; } = "";
        public string Symbol { get; set; } = "";
        public double LastPrice { get; set; }
        public double ChangeRate24h { get; set; }
        public double Volume24h { get; set; }
        public double High24h { get; set; }
        public double Low24h { get; set; }
        public long ExchTimeMs { get; set; }
    }
    // 읽기용 DTO (SQLite 타입에 맞춰 long/double/string)
    public sealed class TradeRow
    {
        public string Exchange { get; set; } = "";
        public string Symbol { get; set; } = "";
        public long TradeId { get; set; }
        public long TradeTimeMs { get; set; }
        public double Price { get; set; }
        public double Qty { get; set; }
        public string Side { get; set; } = "";
    }
}
