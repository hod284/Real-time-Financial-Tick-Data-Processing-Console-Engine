using System;
using System.Collections.Generic;
using System.Text;
using ConsoleApp1.Models;

namespace ConsoleApp1.Telegram
{
    // 명령 실행 결과 (데이터만). 글자·버튼으로 바꾸는 건 TelegramView가 함
    public interface IMessage { }

    // 그냥 안내 문구
    public sealed record TextMessage(string Text) : IMessage;
    // 명령 하나 = 클래스 하나 (Command 패턴)
    // 새 명령은 이걸 구현하고 DI에 등록만 하면 TelegramReceiver가 Key로 찾아서 실행
    public interface ICommand
    {
        string Key { get; }               // 버튼 데이터 맨 앞: "ex", "sym", "val", "ana"
        IMessage Execute(string[] args);
    }
    // 거래소 선택 화면 (Exchange == null) 또는 그 거래소의 구독 목록 화면
    public sealed record ExchangeMenuViewModel(Exchange? Exchange, IReadOnlyList<Subscription> Subscriptions) : IMessage;

    // 종목 하나를 골랐을 때: [체결][호가][캔들][티커][분석] 버튼 화면
    public sealed record SymbolMenuViewModel(Subscription Subscription) : IMessage;
}