using ConsoleApp1.Models;
using Dapper;
using System.Data;

namespace ConsoleApp1.Storage
{
    public sealed class AlertRepository(Database db)
    {
        private const string InsertSql = """
            INSERT INTO alerts (exchange, symbol, rule, message, value, threshold, time_ms)
            VALUES (@Exchange, @Symbol, @Rule, @Message, @Value, @Threshold, @TimeMs);
            """;

        public int Insert(IDbConnection conn, IDbTransaction tx, IEnumerable<Alert> alerts)
        {
            var rows = alerts.Select(a => new
            {
                Exchange = a.Exchange.ToString(),
                a.Symbol,
                a.Rule,
                a.Message,
                a.Value,
                a.Threshold,
                a.TimeMs,
            });
            return conn.Execute(InsertSql, rows, tx);
        }

        // 최근 알림 N건 (최신순)
        //  exchange/symbol을 안 주면 전체 (대시보드 하단)
        //  주면 그 종목만 (텔레그램 [분석] 버튼)
        public IReadOnlyList<AlertRow> GetRecent(int limit = 20, Exchange? exchange = null, string? symbol = null)
        {
            using var conn = db.Open();
            return conn.Query<AlertRow>("""
                SELECT id AS Id, exchange AS Exchange, symbol AS Symbol, rule AS Rule, message AS Message,
                       value AS Value, threshold AS Threshold, time_ms AS TimeMs
                FROM alerts
                WHERE (@Exchange IS NULL OR exchange = @Exchange)
                  AND (@Symbol IS NULL OR symbol = @Symbol)
                ORDER BY time_ms DESC
                LIMIT @Limit;
                """, new { Exchange = exchange?.ToString(), Symbol = symbol, Limit = limit }).AsList();
        }
    }
}