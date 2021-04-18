using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace WebServer
{
    class Program
    {
        static void Main(string[] args)
        {
            WebServerV1 www = new WebServerV1();
            www.responseCodePage = "windows-1251";
            www.staticContent = "..\\..\\StaticFiles";
            www.useEmbeddedResources = true;
            www.AddRoute("/", RouteFunctions.Index);
            www.AddRoute("/login", RouteFunctions.Login);
            www.AddRoute("/logout", RouteFunctions.Logout);

            Console.WriteLine("Ready... Press 'Q' to exit.");

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
 