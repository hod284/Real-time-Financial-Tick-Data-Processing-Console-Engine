using ConsoleApp1.Models;

namespace ConsoleApp1.Event
{
    // 알림을 받을 클래스가 구현 (Observer)
    // 디스패처 스레드에서 호출되므로 빨리 끝나는 일만 할 것
    // 느린 일(네트워크, DB)은 큐에 넣고 바로 리턴
    public interface IAlertObserver
    {
        void OnAlert(Alert alert);
    }
}