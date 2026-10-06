using ConsoleApp1.Models;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

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
        protected abstract void OnMessage(ReadOnlySpan<byte>  payload, long recvMs);

        protected void Emite(MarketEvent mark)
        {
            if(_output != null)
            _output.TryWrite(mark);
        }

        public async Task RunAsync(ChannelWriter<MarketEvent> output, CancellationToken ct)
        {
            _output = output;
            int attempt = 0;
            var buffer = new ArrayBufferWriter<byte>(64 * 1024);

            while (!ct.IsCancellationRequested)                           // 바깥 루프: 재연결용
            {
                try
                {
                    using var ws = new ClientWebSocket();
                    ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);

                    await ws.ConnectAsync(Endpoint, ct);                   // ① 연결
                    await OnConnectedAsync(ws, ct);                        // ② 구독 (자식)
                    attempt = 0;

                    while (ws.State == WebSocketState.Open)                // 안쪽 루프: 수신용
                    {
                        var result = await ws.ReceiveAsync(buffer.GetMemory(16 * 1024), ct);   // ③ 올 때까지 기다림
                        if (result.MessageType == WebSocketMessageType.Close) 
                            break;

                        buffer.Advance(result.Count);// 버퍼 배열 칸수 늘리기
                        if (!result.EndOfMessage) 
                            continue;                // 조각이면 더 모음

                        long recvMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        try 
                        { 
                            OnMessage(buffer.WrittenSpan, recvMs); 
                        }     // ④ 해석 (자식)
                        catch (Exception) 
                        { 
                            /* 해석 실패 로그 */ 
                        }
                        buffer.Clear();
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) // when 조건이 성림할때 catch문에 들어 간다
                { 
                    break; 
                }   // 프로그램 종료
                catch (Exception) 
                {
                    /* 연결 실패·끊김 로그 */ 
                }

                buffer.Clear();
                var delay = TimeSpan.FromMilliseconds(Math.Min(30_000, 500 * Math.Pow(2, attempt++)));
                try 
                {
                    await Task.Delay(delay, ct);
                } 
                catch (OperationCanceledException) 
                { 
                    break; 
                }  // ⑤ 잠깐 쉬고 재연결
            }

        }

    }
}
