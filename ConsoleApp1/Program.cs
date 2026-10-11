using ConsoleApp1.Dashboard;
using ConsoleApp1.singleton;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Serialization;

namespace ConsoleApp1
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = AppContext.BaseDirectory,   // AppSetting.json, wwwroot를 실행 폴더에서 찾음
            });

            builder.Configuration.AddJsonFile("AppSetting.json", optional: false);
            builder.WebHost.UseUrls("http://localhost:5000");

            // enum을 0,1 대신 "Binance", "Buy" 같은 문자열로 (API, SignalR 둘 다)
            builder.Services.ConfigureHttpJsonOptions(o =>
                o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            builder.Services.AddSignalR().AddJsonProtocol(o =>
                o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

            builder.Services.AddMarketBot(builder.Configuration);

            var app = builder.Build();
            app.MapDashboard();
            await app.RunAsync();
        }
    }
}