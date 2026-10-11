using ConsoleApp1.Models;
using Dapper;

namespace ConsoleApp1.Storage
{
    // 구독 목록 저장 → 프로그램을 껐다 켜도 유지
    // 추가/삭제는 가끔 일어나는 일이라 DbWriter 큐를 안 거치고 바로 씀
    public sealed class SubscriptionRepository(Database db)
    {
        public IReadOnlyList<Subscription> GetAll()
        {
            using var conn = db.Open();
            return conn.Query<SubscriptionRow>("""
                SELECT exchange AS Exchange, symbol AS Symbol, name AS Name
                FROM subscriptions
                ORDER BY created_ms;
                """)
                .Select(r => new Subscription(Enum.Parse<Exchange>(r.Exchange), r.Symbol, r.Name))
                .ToList();
        }

        public void Add(Subscription s)
        {
            using var conn = db.Open();
            conn.Execute("""
                INSERT OR IGNORE INTO subscriptions (exchange, symbol, name, created_ms)
                VALUES (@Exchange, @Symbol, @Name, @CreatedMs);
                """, new
            {
                Exchange = s.Exchange.ToString(),
                s.Symbol,
                s.Name,
                CreatedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            });
        }

        public void Remove(Exchange exchange, string symbol)
        {
            using var conn = db.Open();
            conn.Execute("DELETE FROM subscriptions WHERE exchange = @Exchange AND symbol = @Symbol;",
                new { Exchange = exchange.ToString(), Symbol = symbol });
        }
    }
}