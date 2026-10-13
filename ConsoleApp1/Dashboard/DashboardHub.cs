using Microsoft.AspNetCore.SignalR;

namespace ConsoleApp1.Dashboard
{
    // 브라우저가 접속하는 SignalR 통로 (서버 → 브라우저로 밀어주기만 함)
    public sealed class DashboardHub : Hub
    {
    }
}