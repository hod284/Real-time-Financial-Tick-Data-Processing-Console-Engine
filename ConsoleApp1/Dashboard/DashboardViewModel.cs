using ConsoleApp1.Health;
using ConsoleApp1.Models;

namespace ConsoleApp1.Dashboard
{
    // 왼쪽: 수신 상태 + 구독 목록 (2초마다)
    public sealed record OverviewViewModel(IReadOnlyList<FeedStatus> Feeds, IReadOnlyList<Subscription> Subscriptions);

    // 가운데: 선택한 종목의 현재 상태 (1초마다)
    public sealed record SymbolDetailViewModel(Subscription Subscription, SymbolSnapshot Snapshot);

    // 구독 추가 요청: { "exchange": "Upbit", "input": "비트코인" }
    public sealed record AddSubscriptionRequest(Exchange Exchange, string Input);

    // 텔레그램 연결 요청: { "token": "123456:ABC..." }
    public sealed record TelegramTokenRequest(string? Token);
}