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

            context.templateVariables.Add("pcInfo", pcInfo);
            context.templateVariables.Add("date", DateTime.Now.ToString());

            TemplateParser tp = new TemplateParser();
            return new ResponseContext(tp.ParseFromResource("index.html", context.templateVariables));
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
                if (context.GetParam("login") != "")
                {
                    try
                    {
                        PrincipalContext ctx = new PrincipalContext(ContextType.Machine);
                        bool userExists = ctx.ValidateCredentials(context.GetParam("login"), context.GetParam("password"));

                        if (userExists) context.sessionManager.SessionSetKey(ref context.session, "user", context.GetParam("login"));
                    }
                    catch { };
                }
            }

            return new ResponseContext("", "/");
        }

        // Route: "/logout"
        public static ResponseContext Logout(RequestContext context)
        {
            // проверим не запущен ли процесс, и если да - завершим его
            Process p = (Process)context.sessionManager.SessionGetKey(context.session, "process");
            p?.Kill();
            p = null;

            context.sessionManager.SessionClear(ref context.session);
            return new ResponseContext("", "/");
        }

        static ConcurrentQueue<string> outQueue = new ConcurrentQueue<string>();

        // Route: "/cmdline"
        public static ResponseContext Cmdline(RequestContext context)
        {
            // проверим доступ
            if (context.sessionManager.SessionGetKey(context.session, "user") == null)
            {
                // пользователь неавторизован - редирект на главную страницу
                return new ResponseContext("", "/");
            }

            // глобальные переменные в сессии
            Process p = (Process)context.sessionManager.SessionGetKey(context.session, "process");
            string cmd = (string)context.sessionManager.SessionGetKey(context.session, "cmd", "cmd.exe");

            if (context.Method == RequestMethod.GET)
            {
                // отобразим страницу
                context.templateVariables.Add("status", (p == null ? false : (!p.HasExited ? true : false)));
                context.templateVariables.Add("cmd", cmd);

                TemplateParser tp = new TemplateParser();
                return new ResponseContext(tp.ParseFromResource("cmdline.html", context.templateVariables));
            }
            else if (context.Method == RequestMethod.POST)
            {
                if (context.parameters.ContainsKey("btnRun"))
                {
                    // ЗАПРОС ЗАПУСКА КОМАНДЫ

                    p?.Kill(); // завершим процесс, если был открыт

                    cmd = context.GetParam("cmd", "cmd.exe");
                    context.sessionManager.SessionSetKey(ref context.session, "cmd", cmd);

                    // запустим процесс в скрытом окне, перенаправим потоки Stdin и Stdout в наш обработчик
                    p = new Process();
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.RedirectStandardInput = true;
                    p.StartInfo.RedirectStandardError = true;
                    p.StartInfo.FileName = cmd;
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

                    context.sessionManager.SessionSetKey(ref context.session, "process", p);
                    context.templateVariables.Add("status", (p == null ? false : (!p.HasExited ? true : false)));
                    context.templateVariables.Add("cmd", cmd);

                    TemplateParser tp = new TemplateParser();
                    return new ResponseContext(tp.ParseFromResource("cmdline.html", context.templateVariables));
                }
                else if (context.parameters.ContainsKey("btnStop"))
                {
                    // ЗАПРОС ОСТАНОВА КОМАНДЫ

                    p?.Kill();
                    p = null;

                    context.sessionManager.SessionSetKey(ref context.session, "process", p);
                    context.templateVariables.Add("status", (p == null ? false : (!p.HasExited ? true : false)));
                    context.templateVariables.Add("cmd", cmd);

                    TemplateParser tp = new TemplateParser();
                    return new ResponseContext(tp.ParseFromResource("cmdline.html", context.templateVariables));
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

        // Route: "/test"
        public static ResponseContext Test(RequestContext context)
        {
            if (context.GetParamsCount() == 0)
            {
                // запрос чистой страницы
                TemplateParser tp = new TemplateParser();
                string ret = tp.ParseFromString(@"<html><body><a href=""/test?a=1"">set cookie</a><br><a href=""/test?a=2"">clear cookie</a></body></html>");
                return new ResponseContext(ret);
            }
            else
            {
                if (context.GetParam("a") == "1")
                {
                    context.sessionManager.SessionSetKey(ref context.session, "abc", "123");
                }
                else if (context.GetParam("a") == "2")
                {
                    context.sessionManager.SessionClear(ref context.session);
                }
            }

            return new ResponseContext("", "/test");
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
