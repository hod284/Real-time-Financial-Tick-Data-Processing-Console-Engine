using ConsoleApp1.Analysis;
using ConsoleApp1.Dashboard;
using ConsoleApp1.Event;
using ConsoleApp1.Health;
using ConsoleApp1.Models;
using ConsoleApp1.Storage;
using ConsoleApp1.Store;
using ConsoleApp1.Telegram;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleApp1.singleton
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMarketBot(this IServiceCollection services, IConfiguration config)
        {
            // 설정
            services.Configure<MarketOptions>(config.GetSection("Market"));
            services.Configure<TelegramOptions>(config.GetSection("Telegram"));

            // 저장 (DB)
            services.AddSingleton<Database>();
            services.AddSingleton<TradeRepository>();
            services.AddSingleton<CandleRepository>();
            services.AddSingleton<TickerRepository>();
            services.AddSingleton<AlertRepository>();
            services.AddSingleton<SubscriptionRepository>();
            services.AddSingleton<DbWriter>();

            // 구독 / 상태 (메모리)
            services.AddSingleton<SymbolDirectory>();
            services.AddSingleton<SubscriptionService>();
            services.AddSingleton<FeedHealthCheck>();
            services.AddSingleton<MarketStore>();

            // 분석 (Strategy): 룰 추가 = 여기 한 줄
            services.AddSingleton<IAlertRule, PriceSurgeRule>();
            services.AddSingleton<IAlertRule, VolumeSpikeRule>();
            services.AddSingleton<AnalysisEngine>();

            // 알림 받을 곳 (Observer) - ★ 같은 인스턴스를 IAlertObserver로도 꺼내게
            services.AddSingleton<IAlertObserver, ConsoleAlertObserver>();
            services.AddSingleton<IAlertObserver>(sp => sp.GetRequiredService<DbWriter>());
            services.AddSingleton<DashboardNotifier>();
            services.AddSingleton<IAlertObserver>(sp => sp.GetRequiredService<DashboardNotifier>());

            // 텔레그램: 항상 등록. 토큰은 대시보드에서 입력 (없으면 텔레그램만 쉬고 나머지는 정상 동작)
            services.AddSingleton<TelegramSettings>();                  // 토큰/ChatId/봇 클라이언트 보관
            services.AddSingleton<TelegramNotifier>();
            services.AddSingleton<IAlertObserver>(sp => sp.GetRequiredService<TelegramNotifier>());

            // 명령 (Command): 새 버튼 기능 = 여기 한 줄
            services.AddSingleton<ICommand, ExchangeCommand>();
            services.AddSingleton<ICommand, SymbolCommand>();
            services.AddSingleton<ICommand, ValueCommand>();
            services.AddSingleton<ICommand, AnalysisCommand>();
            services.AddHostedService<TelegramReceiver>();

            services.AddSingleton<EventBus>();
            services.AddHostedService<MarketBotHostedService>();
            return services;
        }
    }
}