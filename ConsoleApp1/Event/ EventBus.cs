using ConsoleApp1.Models;

namespace ConsoleApp1.Event
{
    // Subject: 구독자 목록을 들고 있다가 알림이 오면 전부에게 알려줌
    public sealed class EventBus
    {
        private readonly List<IAlertObserver> _observers;

        // DI가 IAlertObserver로 등록된 것들을 전부 넣어줌
        public EventBus(IEnumerable<IAlertObserver> observers)
        {
            _observers = observers.ToList();
        }

        public void Publish(Alert alert)
        {
            foreach (var observer in _observers)
            {
                try
                {
                    observer.OnAlert(alert);
                }
                catch (Exception ex)
                {
                    // 하나가 실패해도 나머지는 받게
                    Console.Error.WriteLine($"[{observer.GetType().Name}] 알림 처리 실패: {ex.Message}");
                }
            }
        }
    }
}