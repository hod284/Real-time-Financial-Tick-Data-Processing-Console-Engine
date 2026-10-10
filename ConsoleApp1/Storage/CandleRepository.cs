using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

namespace ConsoleApp1.Storage
{
      public sealed class CandleRepository(Database db)
    {
        // 캔들은 1분 동안 계속 갱신돼서 옴 → 같은 봉(open_time)이면 INSERT 대신 UPDATE (UPSERT)
        private const string UpsertSql = """
            INSERT INTO candles (exchange, symbol, interval, open_time_ms, open, high, low, close, volume, is_closed, updated_ms)
            VALUES (@Exchange, @Symbol, @Interval, @OpenTimeMs, @Open, @High, @Low, @Close, @Volume, @IsClosed, @UpdatedMs)
            ON CONFLICT (exchange, symbol, interval, open_time_ms) DO UPDATE SET
                open = excluded.open, high = excluded.high, low = excluded.low, close = excluded.close,
                volume = excluded.volume, is_closed = excluded.is_closed, updated_ms = excluded.updated_ms;
            """;

        public int Upsert(IDbConnection conn, IDbTransaction tx, IEnumerable<CandleEvent> candles)
        {
            var rows = candles
                // [선택] 1초 사이에 같은 봉이 여러 번 왔으면 마지막 것만 → 쓰기 횟수 줄이기
                .GroupBy(c => (c.Exchange, c.Symbol, c.Interval, c.OpenTimeMs))
                .Select(g => g.Last())
                .Select(c => new
                {
                    Exchange = c.Exchange.ToString(),
                    c.Symbol,
                    c.Interval,
                    c.OpenTimeMs,
                    Open = (double)c.Open,
                    High = (double)c.High,
                    Low = (double)c.Low,
                    Close = (double)c.Close,
                    Volume = (double)c.Volume,
                    IsClosed = c.IsClosed ? 1 : 0,
                    UpdatedMs = c.RecvTimeMs,
                });
            return conn.Execute(UpsertSql, rows, tx);
        }

        // 최근 캔들 N개 (오래된 것 → 최신 순, 차트 그리기 편하게)
        public IReadOnlyList<CandleRow> GetRecent(Exchange exchange, string symbol, string interval = "1m", int limit = 60)
        {
            using var conn = db.Open();
            var rows = conn.Query<CandleRow>("""
                SELECT exchange AS Exchange, symbol AS Symbol, interval AS Interval, open_time_ms AS OpenTimeMs,
                       open AS Open, high AS High, low AS Low, close AS Close, volume AS Volume, is_closed AS IsClosed
                FROM candles
                WHERE exchange = @Exchange AND symbol = @Symbol AND interval = @Interval
                ORDER BY open_time_ms DESC
                LIMIT @Limit;
                """, new { Exchange = exchange.ToString(), Symbol = symbol, Interval = interval, Limit = limit }).AsList();
            rows.Reverse();   // DESC로 최근 N개 뽑은 뒤 뒤집어서 시간순으로
            return rows;
        }
    }
}