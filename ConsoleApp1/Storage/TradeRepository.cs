using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleApp1.Storage
{
     public sealed class TradeRepository(Database db)
    {
        // OR IGNORE: PK(exchange, symbol, trade_id)가 겹치면 에러 대신 그냥 건너뜀 → 중복 저장 방지
        private const string InsertSql = """
            INSERT OR IGNORE INTO trades (exchange, symbol, trade_id, trade_time_ms, recv_time_ms, price, qty, side)
            VALUES (@Exchange, @Symbol, @TradeId, @TradeTimeMs, @RecvTimeMs, @Price, @Qty, @Side);
            """;

        // 쓰기: DbWriter가 열어둔 연결/트랜잭션 안에서 호출
        public int Insert(IDbConnection conn, IDbTransaction tx, IEnumerable<TradeEvent> trades)
        {
            var rows = trades.Select(t => new
            {
                Exchange = t.Exchange.ToString(),   // enum 그대로 넘기면 0,1 숫자로 저장됨
                t.Symbol,
                t.TradeId,
                TradeTimeMs = t.ExchTimeMs,
                t.RecvTimeMs,
                Price = (double)t.Price,            // SQLite엔 decimal이 없어서 double로
                Qty = (double)t.Qty,
                Side = t.Side.ToString(),
            });
            return conn.Execute(InsertSql, rows, tx);
        }

        // 읽기: 최근 체결 N건 (최신순)
        public IReadOnlyList<TradeRow> GetRecent(Exchange exchange, string symbol, int limit = 100)
        {
            using var conn = db.Open();
            return conn.Query<TradeRow>("""
                SELECT exchange AS Exchange, symbol AS Symbol, trade_id AS TradeId, trade_time_ms AS TradeTimeMs,
                       price AS Price, qty AS Qty, side AS Side
                FROM trades
                WHERE exchange = @Exchange AND symbol = @Symbol
                ORDER BY trade_time_ms DESC
                LIMIT @Limit;
                """, new { Exchange = exchange.ToString(), Symbol = symbol, Limit = limit }).AsList();
        }
    }
}
