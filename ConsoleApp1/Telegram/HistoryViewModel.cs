using ConsoleApp1.Models;
using ConsoleApp1.Storage;

namespace ConsoleApp1.Telegram
{
    // [분석] 버튼 결과: 지금 분석 상태 + 이 종목 최근 알림 10개
    public sealed record HistoryViewModel(
        Subscription Subscription,
        AnalysisSnapshot? Analysis,
        IReadOnlyList<AlertRow> Alerts) : IMessage;
}