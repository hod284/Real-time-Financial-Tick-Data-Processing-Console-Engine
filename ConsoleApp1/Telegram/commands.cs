using ConsoleApp1.Models;
using ConsoleApp1.Storage;
using ConsoleApp1.Store;

namespace ConsoleApp1.Telegram
{
    // "ex"  : 거래소 선택 / 구독 목록       예) ex            ex|Upbit
    public sealed class ExchangeCommand(SubscriptionService subscriptions) : ICommand
    {
        public string Key => "ex";

        public IMessage Execute(string[] args)
        {
            if (args.Length == 0 || !Enum.TryParse<Exchange>(args[0], true, out var ex))
                return new ExchangeMenuViewModel(null, Array.Empty<Subscription>());

            var subs = subscriptions.GetAll().Where(s => s.Exchange == ex).ToList();
            return new ExchangeMenuViewModel(ex, subs);
        }
    }

    // "sym" : 종목 메뉴                     예) sym|Upbit|KRW-BTC
    public sealed class SymbolCommand(SubscriptionService subscriptions) : ICommand
    {
        public string Key => "sym";

        public IMessage Execute(string[] args)
            => CommandArgs.FindSubscription(subscriptions, args, 0) is { } sub
                ? new SymbolMenuViewModel(sub)
                : CommandArgs.NotSubscribed;
    }

    // "val" : 현재 값                       예) val|trade|Upbit|KRW-BTC
    public sealed class ValueCommand(SubscriptionService subscriptions, MarketStore store) : ICommand
    {
        public string Key => "val";

        public IMessage Execute(string[] args)
        {
            if (args.Length == 0 || CommandArgs.FindSubscription(subscriptions, args, 1) is not { } sub)
                return CommandArgs.NotSubscribed;
            return new QuoteViewModel(sub, args[0], store.Get(sub.Exchange, sub.Symbol));
        }
    }

    // "ana" : 분석 상태 + 최근 알림 10개     예) ana|Upbit|KRW-BTC
    public sealed class AnalysisCommand(SubscriptionService subscriptions, MarketStore store, AlertRepository alerts) : ICommand
    {
        public string Key => "ana";

        public IMessage Execute(string[] args)
        {
            if (CommandArgs.FindSubscription(subscriptions, args, 0) is not { } sub)
                return CommandArgs.NotSubscribed;
            return new HistoryViewModel(
                sub,
                store.Get(sub.Exchange, sub.Symbol).Analysis,
                alerts.GetRecent(10, sub.Exchange, sub.Symbol));
        }
    }

    // 명령들이 같이 쓰는 도우미
    internal static class CommandArgs
    {
        public static readonly TextMessage NotSubscribed = new("구독 중인 종목이 아니에요. 다시 골라주세요.");

        // args[start] = 거래소, args[start+1] = 종목 코드
        public static Subscription? FindSubscription(SubscriptionService subs, string[] args, int start)
        {
            if (args.Length < start + 2 || !Enum.TryParse<Exchange>(args[start], true, out var ex))
                return null;
            return subs.Find(ex, args[start + 1]);
        }
    }
}