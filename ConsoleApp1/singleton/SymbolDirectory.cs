using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleApp1.singleton
{
    public sealed class SymbolDirectory
    {
        private Dictionary<string,string> _namecode = new Dictionary<string, string>();
        private Dictionary<string,string> _codeanme = new Dictionary<string,string>();
        public void Add(string name, string code)
        {
            if (!_namecode.TryAdd(name, code))
            {
                _namecode[name] = code;
            }
            if (!_codeanme.TryAdd(code, name))
            { 
               _codeanme [code] = name;
            }
        }
        public string? Findcode(string name)
        { 
        return  _namecode [name];
        }
        public string? Findname(string code)
        {
            return _codeanme[code];
        }
    }
}
