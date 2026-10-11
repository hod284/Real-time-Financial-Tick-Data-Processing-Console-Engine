using ConsoleApp1.Models;
using System.Threading.Channels;

namespace ConsoleApp1.Event
{
    // "큐에 넣고 따로 보내는" 옵저버의 공통 틀 (텔레그램, 대시보드)
    //  - OnAlert : 큐에 넣고 바로 리턴 → 디스패처가 안 막힘
    //  - RunAsync: 별도 루프에서 꺼내서 SendAsync (네트워크라 느린 일)
    //  - 자식은 SendAsync만 구현 (WebSocketFeedBase가 OnMessage만 자식에게 맡기는 것과 같은 방식)
    public abstract class QueuedAlertObserver : IAlertObserver
    {
        private readonly Channel<Alert> _queue =
            Channel.CreateUnbounded<Alert>(new UnboundedChannelOptions { SingleReader = true });

        protected abstract string Name { get; }
        protected abstract Task SendAsync(Alert alert, CancellationToken ct);

        public void OnAlert(Alert alert) 
        {
             _queue.Writer.TryWrite(alert);
        }
        public void Complete() 
        {
            _queue.Writer.TryComplete();
        }
        public async Task RunAsync(CancellationToken ct)
        {
            await foreach (var alert in _queue.Reader.ReadAllAsync(ct))
            {
                try
                {
                    await SendAsync(alert, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Console.Error.WriteLine($"[{Name}] 전송 실패: {ex.Message}");
                }
            }
        }
    }
}