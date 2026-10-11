using ConsoleApp1.Models;
using System.Collections.Concurrent;

namespace ConsoleApp1.Health
{
    public sealed record FeedStatus(Exchange Exchange, string State, double? SecondsSinceLast, int Subscriptions);

    // 거래소별 "마지막으로 데이터 받은 시각" 기록 → 끊겼는지 판단
    // 쓰기: 디스패처(이벤트마다) / 읽기: 웹·텔레그램 → 스레드가 달라서 ConcurrentDictionary
    public sealed class FeedHealthCheck
    {
        private const double StaleSeconds = 10;   // 10초 넘게 아무것도 안 오면 "지연"
        private readonly ConcurrentDictionary<Exchange, long> _lastRecvMs = new();

        public void OnEvent(Exchange ex, long recvMs) 
        {
              _lastRecvMs[ex] = recvMs;
        }
        public FeedStatus Check(Exchange ex, int subscriptionCount)
        {
            if (subscriptionCount == 0)
                return new FeedStatus(ex, "구독 없음", null, 0);
            if (!_lastRecvMs.TryGetValue(ex, out var last))
                return new FeedStatus(ex, "수신 없음", null, subscriptionCount);

            double age = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - last) / 1000.0;
            return new FeedStatus(ex, age <= StaleSeconds ? "정상" : "지연", Math.Round(age, 1), subscriptionCount);
        }
    }
}