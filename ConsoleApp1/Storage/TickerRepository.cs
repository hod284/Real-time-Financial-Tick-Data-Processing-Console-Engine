using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleApp1.Storage
{
    // 티커는 이력을 쌓지 않고 종목별 "마지막 상태 1줄"만 유지 (UPSERT)
    public sealed class TickerRepository(Database db)
    {
     private const string UpsertSql = """
            INSERT INTO ticker_latest (exchange, symbol, last_price, change_rate_24h, volume_24h, high_24h, low_24h, exch_time_ms)
            VALUES (@Exchange, @Symbol, @LastPrice, @ChangeRate24h, @Volume24h, @High24h, @Low24h, @ExchTimeMs)
            ON CONFLICT (exchange, symbol) DO UPDATE SET
                last_price = excluded.last_price, change_rate_24h = excluded.change_rate_24h,
                volume_24h = excluded.volume_24h, high_24h = excluded.high_24h, low_24h = excluded.low_24h,
                exch_time_ms = excluded.exch_time_ms;
            """;

        public int Upsert(IDbConnection conn, IDbTransaction tx, IEnumerable<TickerEvent> tickers)
        {
            var rows = tickers
                // 업비트는 체결마다 티커가 와서 1초에 여러 번 옴 → 종목별 마지막 것만
                .GroupBy(t => (t.Exchange, t.Symbol))
                .Select(g => g.Last())
                .Select(t => new
                {
                    Exchange = t.Exchange.ToString(),
                    t.Symbol,
                    LastPrice = (double)t.LastPrice,
                    t.ChangeRate24h,                 // 원래 double이라 변환 X
                    Volume24h = (double)t.Volume24h,
                    High24h = (double)t.High24h,
                    Low24h = (double)t.Low24h,
                    t.ExchTimeMs,
                });
            return conn.Execute(UpsertSql, rows, tx);
        }

        // 읽기: 전 종목 현재 상태 → 대시보드, 텔레그램 /quote 용
        public IReadOnlyList<TickerRow> GetAll()
        {
            using var conn = db.Open();
            return conn.Query<TickerRow>("""
                SELECT exchange AS Exchange, symbol AS Symbol, last_price AS LastPrice, change_rate_24h AS ChangeRate24h,
                       volume_24h AS Volume24h, high_24h AS High24h, low_24h AS Low24h, exch_time_ms AS ExchTimeMs
                FROM ticker_latest
                ORDER BY exchange, symbol;
                """).AsList();
        }
    }
}
