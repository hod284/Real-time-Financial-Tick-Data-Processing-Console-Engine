using ConsoleApp1.Models;

namespace ConsoleApp1.Telegram
{
    // [체결]/[호가]/[캔들]/[티커] 버튼 결과
    //  Kind: "trade", "book", "candle", "ticker"
    public sealed record QuoteViewModel(Subscription Subscription, string Kind, SymbolSnapshot Snapshot) : IMessage;
}