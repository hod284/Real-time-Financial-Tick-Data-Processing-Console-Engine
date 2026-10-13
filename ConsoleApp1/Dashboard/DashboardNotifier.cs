using ConsoleApp1.Event;
using ConsoleApp1.Models;
using Microsoft.AspNetCore.SignalR;

namespace ConsoleApp1.Dashboard
{
    // 알림 → 열려 있는 모든 브라우저로 즉시 전송 (index.html의 conn.on("alert", ...)이 받음)
    public sealed class DashboardNotifier(IHubContext<DashboardHub> hub) : QueuedAlertObserver
    {
        protected override string Name => "Dashboard";

        protected override Task SendAsync(Alert alert, CancellationToken ct)
            => hub.Clients.All.SendAsync("alert", alert, ct);
    }
}