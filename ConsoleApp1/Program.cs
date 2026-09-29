using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ConsoleApp1
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var bulider = Host.CreateDefaultBuilder(args);
        }
    }
}
