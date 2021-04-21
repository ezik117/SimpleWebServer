using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Management;
using System.DirectoryServices.AccountManagement;
using System.Diagnostics;
using System.Net;
using System.Collections.Concurrent;

namespace WebServer
{
    /// <summary>
    /// Функции маршрутов
    /// </summary>
    static class RouteFunctions
    {
        // Route: "/"
        public static ResponseContext Index(RequestContext context)
        {
            Dictionary<string, string> pcInfo = new Dictionary<string, string>();

            // get WMI
            ManagementObjectCollection components;

            components = GetWmi_ClassData("Win32_SystemEnclosure");
            pcInfo.Add("PC Manufacturer", GetWmi_SingleValue("Manufacturer", components));
            pcInfo.Add("PC SerialNumber", GetWmi_SingleValue("SerialNumber", components));

            components = GetWmi_ClassData("Win32_ComputerSystem");
            pcInfo.Add("PC Model", GetWmi_SingleValue("Model", components));

            components = GetWmi_ClassData("Win32_OperatingSystem");
            pcInfo.Add("OS Name", TemplateParser.ConvertString(GetWmi_SingleValue("Caption", components), "UTF-8", "windows-1251"));
            pcInfo.Add("OS Service Pack", GetWmi_SingleValue("CSDVersion", components));

            components = GetWmi_ClassData("Win32_Processor");
            pcInfo.Add("CPU Name", GetWmi_SingleValue("Name", components));
            pcInfo.Add("CPU Manufacturer", GetWmi_SingleValue("Manufacturer", components));

            context.variables.Add("pcInfo", pcInfo);
            context.variables.Add("date", DateTime.Now.ToString());

            TemplateParser tp = new TemplateParser();
            return new ResponseContext(tp.ParseFromResource("index.html", context.variables));
        }

        private static ManagementObjectCollection GetWmi_ClassData(string @class)
        {
            ManagementObjectSearcher mgmtObjSearcher = new ManagementObjectSearcher($"SELECT * FROM {@class}");
            return mgmtObjSearcher.Get();
        }

        private static string GetWmi_SingleValue(string key, ManagementObjectCollection components)
        {
            foreach (ManagementObject component in components)
            {
                try
                {
                    return component[key].ToString();
                }
                catch { };
            }

            return "Wrong key number";
        }

        // Route: "/login"
        public static ResponseContext Login(RequestContext context)
        {
            if (context.Method == RequestMethod.POST)
            {
                if (context.GetValue("login") != "")
                {
                    try
                    {
                        PrincipalContext ctx = new PrincipalContext(ContextType.Machine);
                        bool userExists = ctx.ValidateCredentials(context.GetValue("login"), context.GetValue("password"));

                        if (userExists) context.session.Set("user", context.GetValue("login"));
                    }
                    catch { };
                }
            }

            return new ResponseContext("", "/");
        }

        // Route: "/logout"
        public static ResponseContext Logout(RequestContext context)
        {
            context.sessionManager.DeleteSession(context.session.sessionId);
            return new ResponseContext("", "/");
        }

        static ConcurrentQueue<string> outQueue = new ConcurrentQueue<string>();

        // Route: "/cmdline"
        public static ResponseContext Cmdline(RequestContext context)
        {
            // проверим доступ
            if (context.session.GetString("user") == null)
            {
                // доступ запрещен
                return new ResponseContext("", "", HttpStatusCode.Forbidden);
            }

            if (context.Method == RequestMethod.GET)
            {
                // глобальные переменные в сессии
                Process p = (Process)context.session.Get("process", null);

                // переменные для шаблонизатора
                context.variables.Add("status", (p == null ? false : (!p.HasExited ? true : false)));
                context.variables.Add("cmd", "cmd.exe");

                TemplateParser tp = new TemplateParser();
                tp.enableDebug = true;
                return new ResponseContext(tp.ParseFromResource("cmdline.html", context.variables));
            }
                else if (context.Method == RequestMethod.POST)
            {
                if (context.parameters.ContainsKey("btnRun"))
                {
                    // ЗАПРОС ЗАПУСКА КОМАНДЫ
                    
                    // глобальные переменные в сессии
                    Process p = (Process)context.session.Get("process", null);
                    p?.Kill(); // завершим процесс, если был открыт

                    // запустим процесс в скрытом окне, перенаправим потоки Stdin и Stdout в наш обработчик
                    p = new Process();
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.RedirectStandardInput = true;
                    p.StartInfo.RedirectStandardError = true;
                    //p.StartInfo.FileName = (string)req.values["process"];
                    p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    p.StartInfo.CreateNoWindow = true;
                    p.OutputDataReceived += P_OutputDataReceived;
                    p.ErrorDataReceived += P_OutputDataReceived;
                    try
                    {
                        p.Start();
                        p.BeginOutputReadLine();
                        p.BeginErrorReadLine();
                        p.StandardInput.WriteLine();
                    }
                    catch
                    {
                        p = null;
                    }
                }
            }

            return new ResponseContext("", "");
        }

        // получены данные из STDOUT
        private static void P_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            //ConcurrentQueue<string> outQueue.

            if (e.Data != null)
            {
                outQueue.Enqueue(e.Data + "\r\n");
            }
        }
    }
}

/*
 ПРИМЕРЫ ПОЛЬЗОВАТЕЛЬСКИХ ФУНКЦИЙ:

--- ПУСТАЯ ЗАГОТОВКА ---
// Route: "/"
public static ResponseContext Index(RequestContext context)
{
    return new ResponseContext("");
}

--- РЕДИРЕКТ НА ГЛАВНУЮ ---
return new ResponseContext("", "/");

--- ОБРАБОТКА ШАБЛОНА ИЗ РЕСУРСОВ ---
Dictionary<string, object> data = new Dictionary<string, object>();
data.Add("some_key", some_value);
TemplateParser tp = new TemplateParser(data);
return new ResponseContext(tp.ParseFromResource("index.html", data));

--- СТРАНИЦА НЕ НАЙДЕНА ---
return new ResponseContext("Page is not found", "", HttpStatusCode.NotFound);

--- ИСПОЛЬЗОВАНИЕ СЕССИЙ ---
при использовании переменных сессий в шаблоне, требуется ручная передача объекта сессии
data.Add("session", context.session.keys);

     */
