using ConsoleApp1.Models;
using System.Text;
using Telegram.Bot.Types.ReplyMarkups;

namespace ConsoleApp1.Telegram
{
    public sealed record TelegramReply(string Text, InlineKeyboardMarkup? Keyboard);

    // ViewModel(데이터) → 텔레그램 메시지(글자 + 버튼)
    // 모양을 바꾸고 싶으면 여기만 고치면 됨 (명령 로직은 그대로)
    public static class TelegramView
    {
        public static TelegramReply Render(IMessage message) => message switch
        {
            ExchangeMenuViewModel m => RenderExchangeMenu(m),
            SymbolMenuViewModel m   => new($"{Title(m.Subscription)}\n무엇을 볼까요?", SymbolKeyboard(m.Subscription)),
            QuoteViewModel q        => new(RenderQuote(q), SymbolKeyboard(q.Subscription)),     // 값 + 같은 메뉴 다시
            HistoryViewModel h      => new(RenderHistory(h), SymbolKeyboard(h.Subscription)),
            TextMessage t           => new(t.Text, ExchangeKeyboard()),
            _                       => new(message.ToString() ?? "", null),
        };

        // 알림 메시지 밑에 붙는 버튼 (TelegramNotifier에서 사용)
        public static InlineKeyboardMarkup OpenSymbolButton(Exchange ex, string symbol)
        {
             return  Keyboard(new[] { Button("🔎 이 종목 보기", "sym", ex.ToString(), symbol) });
        }
        // ───── 화면별 ─────

        private static TelegramReply RenderExchangeMenu(ExchangeMenuViewModel m)
        {
            if (m.Exchange is null)
                return new("거래소를 고르세요.", ExchangeKeyboard());
            if (m.Subscriptions.Count == 0)
                return new($"{m.Exchange} 구독 종목이 없어요. 대시보드에서 추가하세요.", ExchangeKeyboard());

            var rows = m.Subscriptions
                .Chunk(2)   // 한 줄에 2개씩
                .Select(pair => pair.Select(s => Button(s.Name, "sym", s.Exchange.ToString(), s.Symbol)).ToArray())
                .Append(new[] { Button("◀ 거래소", "ex") })
                .ToArray();
            return new($"{m.Exchange} 구독 목록 ({m.Subscriptions.Count})", Keyboard(rows));
        }

        private static string RenderQuote(QuoteViewModel q)
        {
            var s = q.Snapshot;
            string head = Title(q.Subscription);
            return q.Kind switch
            {
                "trade" => s.Trade is { } t
                    ? $"💱 체결 · {head}\n가격 {N(t.Price)}\n수량 {N(t.Qty)}\n방향 {SideText(t.Side)}\n시각 {Time(t.ExchTimeMs)}"
                    : NoData(head),
                "book" => s.OrderBook is { Bids: { } bids, Asks: { } asks }
                    ? RenderBook(head, bids, asks)
                    : NoData(head),
                "candle" => s.Candle is { } c
                    ? $"🕯 1분봉 · {head}\n시작 {Time(c.OpenTimeMs)}\n시가 {N(c.Open)}\n고가 {N(c.High)}\n저가 {N(c.Low)}\n종가 {N(c.Close)}\n거래량 {N(c.Volume)}"
                    : NoData(head),
                "ticker" => s.Ticker is { } k
                    ? $"📊 24시간 · {head}\n현재가 {N(k.LastPrice)}\n변동률 {Pct(k.ChangeRate24h * 100)}\n고가 {N(k.High24h)}\n저가 {N(k.Low24h)}\n거래량 {N(k.Volume24h)}"
                    : NoData(head),
                _ => "알 수 없는 항목이에요.",
            };
        }

        private static string RenderBook(string head, Level[] bids, Level[] asks)
        {
            var sb = new StringBuilder($"📖 호가 (상위 5) · {head}\n");
            foreach (var a in asks.Take(5).Reverse())        // 매도: 비싼 것이 위
                sb.AppendLine($"매도 {N(a.Price)} × {N(a.Qty)}");
            sb.AppendLine("──────────");
            foreach (var b in bids.Take(5))                  // 매수: 비싼 것이 위
                sb.AppendLine($"매수 {N(b.Price)} × {N(b.Qty)}");
            return sb.ToString();
        }

        private static string RenderHistory(HistoryViewModel h)
        {
            var sb = new StringBuilder($"🧠 분석 · {Title(h.Subscription)}\n");
            if (h.Analysis is { } a)
            {
                sb.AppendLine($"60초 저가 {N(a.WindowLow)} / 고가 {N(a.WindowHigh)}");
                sb.AppendLine($"저점 대비 {Pct(a.RiseFromLowPct)} · 고점 대비 {Pct(a.FallFromHighPct)}");
                sb.AppendLine(a.CompletedMinutes > 0
                    ? $"이번 1분 거래량: 평균의 {a.VolumeRatio:F1}배 (최근 {a.CompletedMinutes}분)"
                    : "거래량 평균: 아직 쌓이는 중");
            }
            else
            {
                sb.AppendLine("아직 체결이 없어서 분석 전이에요.");
            }

            sb.AppendLine();
            sb.AppendLine(h.Alerts.Count == 0 ? "최근 알림 없음" : $"🔔 최근 알림 {h.Alerts.Count}건");
            foreach (var al in h.Alerts)
                sb.AppendLine($"{DateTime(al.TimeMs)}  {al.Message}");
            return sb.ToString();
        }

        // ───── 버튼 ─────

        private static InlineKeyboardMarkup ExchangeKeyboard()
            => Keyboard(new[] { Button("바이낸스", "ex", "Binance"), Button("업비트", "ex", "Upbit") });

        private static InlineKeyboardMarkup SymbolKeyboard(Subscription s)
        {
            string ex = s.Exchange.ToString(), sym = s.Symbol;
            return Keyboard(
                new[] { Button("체결", "val", "trade", ex, sym), Button("호가", "val", "book", ex, sym) },
                new[] { Button("캔들", "val", "candle", ex, sym), Button("티커", "val", "ticker", ex, sym) },
                new[] { Button("🧠 분석", "ana", ex, sym) },
                new[] { Button("◀ 목록", "ex", ex) });
        }

        // 버튼 하나: 누르면 "key|arg1|arg2..." 가 TelegramReceiver로 옴 (최대 64바이트)
        private static InlineKeyboardButton Button(string text, params string[] data)
            => InlineKeyboardButton.WithCallbackData(text, string.Join('|', data));

        private static InlineKeyboardMarkup Keyboard(params InlineKeyboardButton[][] rows) => new(rows);

        // ───── 글자 모양 ─────

        private static string Title(Subscription s) 
        {
            return $"{s.Name} ({s.Symbol})";
        }
        private static string NoData(string head) 
        {
            return  $"{head}\n아직 데이터가 없어요. 잠시 후 다시 눌러주세요.";
        }
        private static string N(decimal v) 
        {
           return  v.ToString("#,##0.########");
        }
        private static string Pct(double v) 
        {
            return v.ToString("+0.00;-0.00;0.00") + "%";
        }
        private static string SideText(Side s) 
        {
            return s == Side.Buy ? "매수" : s == Side.Sell ? "매도" : "-";
        }
        private static string Time(long ms) 
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().ToString("HH:mm:ss");
        }
        private static string DateTime(long ms) 
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().ToString("MM-dd HH:mm:ss");
        }
    }
}