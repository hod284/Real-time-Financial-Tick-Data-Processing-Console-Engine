using System;
using System.Collections.Generic;
using System.Text;
using ConsoleApp1.Models;

namespace ConsoleApp1.singleton
{
    //업비트랑 바이넨스에서 받은 종목을 한글이름으로 한글이름을 코드로 변환 
    public sealed class SymbolDirectory
    {
        private Dictionary<(Exchange,string),string> _namecode = new Dictionary<(Exchange,string), string>();
        private Dictionary<(Exchange, string), string> _codeanme = new Dictionary<(Exchange, string), string>();

        public void Add(Exchange ex, string name, string code)
        {
             _namecode[(ex,name)] = code;
            if (!_codeanme.TryAdd((ex,code), name))
            { 
               _codeanme [(ex,code)] = name;
            }
        }
        public string? Findcode(Exchange ex, string name)
        { 
           return  _namecode [(ex,name)];
        }
        public string? Findname(Exchange ex,string code)
        {
            return _codeanme[(ex, code)];
        }
        public IReadOnlyList<string> CodesOf(Exchange ex)
        { 
           return _namecode.Where(x=> x.Key.Item1 == ex).Select(x=>x.Value).ToList(); 
        }
        public IReadOnlyList<string> NameOf(Exchange ex)
        { 
           return _codeanme.Where(x=> x.Key.Item1 == ex).Select(x=>x.Value).ToList(); 
        }
// 대소문자 무시 비교 (btcusdt == BTCUSDT)
        private sealed class KeyComparer : IEqualityComparer<(Exchange, string)>
        {
             public bool Equals((Exchange, string) a, (Exchange, string) b)
             {
                a.Item1 == b.Item1 && string.Equals(a.Item2, b.Item2, StringComparison.OrdinalIgnoreCase);
             }
            public int GetHashCode((Exchange, string) k) 
            {
              HashCode.Combine(k.Item1, StringComparer.OrdinalIgnoreCase.GetHashCode(k.Item2));
            }
        }

    }
}
