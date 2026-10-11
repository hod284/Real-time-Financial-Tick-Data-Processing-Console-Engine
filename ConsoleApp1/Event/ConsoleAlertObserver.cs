using ConsoleApp1.Models;

namespace ConsoleApp1.Event
{
    public sealed class ConsoleAlertObserver : IAlertObserver
    {
        public void OnAlert(Alert alert) 
        {
             Console.WriteLine(alert.Message);
        }
    }
}