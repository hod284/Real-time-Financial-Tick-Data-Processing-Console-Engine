using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

namespace ConsoleApp1.Analysis
{
    // 룰 하나 = 클래스 하나 (Strategy 패턴)
    // 새 룰은 이 인터페이스를 구현하고 DI에 등록만 하면 됨 → AnalysisEngine은 안 고쳐도 됨
    public interface IAlertRule
    {
        string Name { get; }

        // 체결 하나 들어올 때마다 호출됨 (state는 이미 이 체결이 반영된 상태)
        // 조건 만족하면 Alert, 아니면 null
        Alert? Evaluate(TradeEvent trade, SymbolState state);
    }
}