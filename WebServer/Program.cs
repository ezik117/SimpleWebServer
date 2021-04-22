using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace WebServer
{
    class Program
    {
        static void Main(string[] args)
        {
            //WebServerV1 www = new WebServerV1();
            //www.responseCodePage = "windows-1251";
            //www.staticContent = "..\\..\\StaticFiles";
            //www.useEmbeddedResources = true;
            //www.AddRoute("/", RouteFunctions.Index);
            //www.AddRoute("/login", RouteFunctions.Login);
            //www.AddRoute("/logout", RouteFunctions.Logout);
            //www.AddRoute("/cmdline", RouteFunctions.Cmdline);
            //www.AddRoute("/test", RouteFunctions.Test);

            //Console.WriteLine("Ready... Press 'Q' to exit.");
            //Console.WriteLine("Usage http://localhost:8080");

            System.Data.DataSet ds = new System.Data.DataSet();
            ds.Tables.Add("sampleTable");


            Dictionary<string, object> vals = new Dictionary<string, object>();
            vals.Add("s1", "hello");
            vals.Add("s2", "world");

            TemplateParser tp = new TemplateParser(vals);
            string exp = "d1=s1";
            string r = tp.EvaluateExpression(exp).ToString();
            exp = "d1";
            r = tp.EvaluateExpression(exp).ToString();
            Console.WriteLine($"'{exp}' = {r}");


            ConsoleKeyInfo k = Console.ReadKey(true);
            while (k.Key != ConsoleKey.Q)
            {
                if (k.Key == ConsoleKey.Q)
                {
                    //www.Stop();
                    break;
                }

                    Console.WriteLine();
                k = Console.ReadKey(true);
            }
        }
    }

}
 