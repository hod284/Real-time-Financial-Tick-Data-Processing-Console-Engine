using ConsoleApp1.Health;
using ConsoleApp1.Models;
using ConsoleApp1.Storage;
using ConsoleApp1.Store;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ConsoleApp1.Dashboard
{
    // 웹 주소 등록 모음
    public static class DashboardService
    {
        public static WebApplication MapDashboard(this WebApplication app)
        {
            app.UseDefaultFiles();   // "/" → index.html
            app.UseStaticFiles();    // wwwroot

            // 왼쪽 위 수신 상태 + 사이드바 구독 목록
            app.MapGet("/api/overview", (SubscriptionService subs, FeedHealthCheck health) =>
            {
                var all = subs.GetAll();
                var feeds = new[] { Exchange.Binance, Exchange.Upbit }
                    .Select(ex => health.Check(ex, all.Count(s => s.Exchange == ex)))
                    .ToList();
                return new OverviewViewModel(feeds, all);
            });

            // 선택한 종목의 현재 상태 (체결/호가/캔들/티커/분석)
            app.MapGet("/api/symbols/{exchange}/{symbol}",
                (Exchange exchange, string symbol, SubscriptionService subs, MarketStore store) =>
                    subs.Find(exchange, symbol) is { } sub
                        ? Results.Ok(new SymbolDetailViewModel(sub, store.Get(sub.Exchange, sub.Symbol)))
                        : Results.NotFound());

            // 하단 알림 (처음 열 때)
            app.MapGet("/api/alerts", (AlertRepository alerts, int? limit) => alerts.GetRecent(limit ?? 50));

            // 구독 추가 / 삭제
            app.MapPost("/api/subscriptions", (AddSubscriptionRequest req, SubscriptionService subs) =>
                subs.TryAdd(req.Exchange, req.Input, out var message)
                    ? Results.Ok(new { message })
                    : Results.BadRequest(new { message }));

            app.MapDelete("/api/subscriptions/{exchange}/{symbol}",
                (Exchange exchange, string symbol, SubscriptionService subs) =>
                    subs.Remove(exchange, symbol)
                        ? Results.Ok(new { message = $"{symbol} 구독 해제" })
                        : Results.NotFound(new { message = $"{symbol}은 구독 중이 아니에요" }));

            // 새 알림 실시간 push
            app.MapHub<DashboardHub>("/hub/dashboard");
            return app;
        }
    }
}