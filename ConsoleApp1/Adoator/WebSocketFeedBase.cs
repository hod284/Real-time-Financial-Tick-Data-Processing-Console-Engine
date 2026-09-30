using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using ConsoleApp1.Models;

namespace ConsoleApp1.Adoator
{
    public abstract class WebSocketFeedBase
    {

        private ChannelWriter<MarketEvent>? _output;
        protected abstract string Name { get; }
        protected abstract Uri Endpoint { get; }
        protected virtual Task OnConnectedAsync(ClientWebSocket ws, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
        protected abstract void OnMessage(ReadOnlyMemory<byte> payload, long recvMs);

        protected void Emite(MarketEvent mark)
        {
            if(_output != null)
            _output.TryWrite(mark);
        }

        public async Task RunAsync()
        { 
        
        
        }

    }
}
