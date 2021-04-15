using System;

namespace WebServer
{
    class Program
    {
        static void Main(string[] args)
        {

            WebServerV1 www = new WebServerV1();
            www.responseCodePage = "windows-1251";
            www.AddRoute("/", RouteFunctions.Index);

            Console.WriteLine("Ready...");

            ConsoleKeyInfo k = Console.ReadKey(true);
            while (k.Key != ConsoleKey.Q)
            {
                if (k.Key == ConsoleKey.Q)
                {
                    www.Stop();
                    break;
                }

                    Console.WriteLine();
                k = Console.ReadKey(true);
            }
        }
    }

}
 