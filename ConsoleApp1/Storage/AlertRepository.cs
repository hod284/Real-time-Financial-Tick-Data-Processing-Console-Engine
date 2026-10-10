using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

namespace ConsoleApp1.Storage
{
    public sealed class AlertRepository(Database db)
    {
        private const string InsertSql = """
            INSERT INTO alerts (exchange, symbol, rule, message, value, threshold, time_ms)
            VALUES (@Exchange, @Symbol, @Rule, @Message, @Value, @Threshold, @TimeMs);
            """;

        // 쓰기: DbWriter가 열어둔 연결/트랜잭션 안에서 호출
        public int Insert(IDbConnection conn, IDbTransaction tx, IEnumerable<Alert> alerts)
        {
            var rows = alerts.Select(a => new
            {
                Exchange = a.Exchange.ToString(),   // enum 그대로 넘기면 0,1 숫자로 저장됨 → 문자열로
                a.Symbol,
                a.Rule,
                a.Message,
                a.Value,
                a.Threshold,
                a.TimeMs,
            });
            return conn.Execute(InsertSql, rows, tx);   // 리스트를 넘기면 Dapper가 건마다 실행
        }

        // 읽기: 최근 알림 N건 (최신순) → 텔레그램 /history, 대시보드용
        public IReadOnlyList<AlertRow> GetRecent(int limit = 20)
        {
            using var conn = db.Open();
            return conn.Query<AlertRow>("""
                SELECT id AS Id, exchange AS Exchange, symbol AS Symbol, rule AS Rule, message AS Message,
                       value AS Value, threshold AS Threshold, time_ms AS TimeMs
                FROM alerts
                ORDER BY time_ms DESC
                LIMIT @Limit;
                """, new { Limit = limit }).AsList();
        }
    }
}