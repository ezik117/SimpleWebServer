using System;

namespace WebServer
{
    class Program
    {
        static void Main(string[] args)
        {

            WebServerV1 www = new WebServerV1();
            www.responseCodePage = "windows-1251";
            www.AddRoute("/", routes.Index);
            www.AddRoute("/a", routes.a);

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

    static class routes
    {
        public static string Index(RequestContext context) // Route: "/"
        {
            if (context.Method == RequestMethod.GET)
            {
                string user = context.session.Get("user", "unknown");
                if (user == "unknown") context.session.Set("user", "Андрей");
                return $@"<HTML><BODY>
                            Route: '/'<BR>
                            User: {user}
                    <BODY></HTML>";
            }

            return "";
        }

        public static string a(RequestContext context) // Route: "/a"
        {
            if (context.Method == RequestMethod.GET)
            {
                string user = context.session.Get("user", "unknown");
                return $@"<HTML><BODY>
                            Route: '/a'<BR>
                            User: {user}
                    <BODY></HTML>";
            }

            return "";
        }
    }
}
 