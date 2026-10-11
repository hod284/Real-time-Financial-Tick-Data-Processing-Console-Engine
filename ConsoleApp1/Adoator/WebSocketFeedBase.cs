using ConsoleApp1.Models;
using System.Buffers;
using System.Net.WebSockets;
using System.Threading.Channels;

namespace ConsoleApp1.Adoator
{
    public abstract class WebSocketFeedBase
    {
        private ChannelWriter<MarketEvent>? _output;
        private CancellationTokenSource? _connCts;    // "지금 연결만" 끊는 스위치 (프로그램 종료와 별개)

        protected abstract string Name { get; }
        protected abstract Uri Endpoint { get; }       // 연결할 때마다 다시 읽음 → 바뀐 구독 반영
        protected abstract bool HasTargets { get; }    // 구독 종목이 하나라도 있나
        protected virtual Task OnConnectedAsync(ClientWebSocket ws, CancellationToken ct) => Task.CompletedTask;
        protected abstract void OnMessage(ReadOnlySpan<byte> payload, long recvMs);

        protected void Emite(MarketEvent e) => _output?.TryWrite(e);

        // 구독이 바뀌면 호출 → 지금 연결을 끊고 새 목록으로 바로 다시 연결
        public void Reconnect()
        {
            try { _connCts?.Cancel(); }
            catch (ObjectDisposedException) { }   // 마침 연결이 바뀌는 순간이면 무시
        }

        public async Task RunAsync(ChannelWriter<MarketEvent> output, CancellationToken ct)
        {
            _output = output;
            int attempt = 0;
            var buffer = new ArrayBufferWriter<byte>(64 * 1024);

            while (!ct.IsCancellationRequested)                        // 바깥 루프: 재연결용
            {
                // 프로그램 종료(ct) 또는 Reconnect() 둘 중 하나만 와도 취소되는 토큰
                using var connCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                _connCts = connCts;
                var token = connCts.Token;
                buffer.Clear();

                try
                {
                    if (!HasTargets)
                    {
                        Console.WriteLine($"[{Name}] 구독 종목 없음 → 추가될 때까지 대기");
                        await Task.Delay(Timeout.Infinite, token);     // Reconnect()가 깨워줌
                    }

                    using var ws = new ClientWebSocket();
                    ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);

                    await ws.ConnectAsync(Endpoint, token);             // ① 연결
                    await OnConnectedAsync(ws, token);                  // ② 구독
                    attempt = 0;
                    Console.WriteLine($"[{Name}] 연결됨");

                    while (ws.State == WebSocketState.Open)             // 안쪽 루프: 수신
                    {
                        var result = await ws.ReceiveAsync(buffer.GetMemory(16 * 1024), token);   // ③
                        if (result.MessageType == WebSocketMessageType.Close)
                            break;

                        buffer.Advance(result.Count);
                        if (!result.EndOfMessage)
                            continue;

                        long recvMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        try
                        {
                            OnMessage(buffer.WrittenSpan, recvMs);      // ④ 해석
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"[{Name}] 해석 실패: {ex.Message}");
                        }
                        buffer.Clear();
                    }
                }
                catch (Exception) when (ct.IsCancellationRequested)
                {
                    break;                                              // 프로그램 종료
                }
                catch (Exception) when (connCts.IsCancellationRequested)
                {
                    Console.WriteLine($"[{Name}] 구독 변경 → 재연결");
                    continue;                                           // 기다리지 않고 바로 다시 연결
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[{Name}] 연결 끊김, 재연결 시도 #{attempt + 1}: {ex.Message}");
                }

                var delay = TimeSpan.FromMilliseconds(Math.Min(30_000, 500 * Math.Pow(2, attempt++)));   // ⑤
                try { await Task.Delay(delay, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}