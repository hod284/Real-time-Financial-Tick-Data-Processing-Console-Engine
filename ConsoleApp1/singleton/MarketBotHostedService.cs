using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleApp1.singleton
{
    // 싱글톤 모듈 시작과 끝을 여기서 정리
    public sealed class MarketBotHostedService() : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
        }
        public override async Task StopAsync(CancellationToken ct)
        {
            await base.StopAsync(ct);
        }
    }
}
