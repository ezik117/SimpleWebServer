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
        // ----------------------------------------------------------------------------------------
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
            pcInfo.Add("OS Name", GetWmi_SingleValue("Caption", components));
            pcInfo.Add("OS Service Pack", GetWmi_SingleValue("CSDVersion", components));

            components = GetWmi_ClassData("Win32_Processor");
            pcInfo.Add("CPU Name", GetWmi_SingleValue("Name", components));

            long memSize = 0;
            components = GetWmi_ClassData("Win32_PhysicalMemory");
            foreach (ManagementObject component in components)
            {
                if (long.TryParse(component["Capacity"].ToString(), out long mem))
                {
                    memSize += mem;
                }
            }
            pcInfo.Add("RAM Size (Gb)", (memSize >> 30).ToString());

            components = GetWmi_ClassData("Win32_BaseBoard");
            pcInfo.Add("MB Manufacturer", GetWmi_SingleValue("Manufacturer", components));
            pcInfo.Add("MB Model", GetWmi_SingleValue("Model", components));
            pcInfo.Add("MB Product", GetWmi_SingleValue("Product", components));
            pcInfo.Add("MB SerialNumber", GetWmi_SingleValue("SerialNumber", components));

            context.templateVariables.Add("pcInfo", pcInfo);
            context.templateVariables.Add("date", DateTime.Now.ToString());

            TemplateParser tp = new TemplateParser();
            return new ResponseContext(tp.ParseFromResource("index.html", context.templateVariables));
        }


        // --------------------------------------------------------------------
        private static ManagementObjectCollection GetWmi_ClassData(string @class)
        {
            ManagementObjectSearcher mgmtObjSearcher = new ManagementObjectSearcher($"SELECT * FROM {@class}");
            return mgmtObjSearcher.Get();
        }


        // --------------------------------------------------------------------
        private static string GetWmi_SingleValue(string key, ManagementObjectCollection components)
        {
            foreach (ManagementObject component in components)
            {
                try
                {
                    return component[key]?.ToString();
                }
                catch { };
            }

            return "Wrong key number";
        }


        // ----------------------------------------------------------------------------------------
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

                        if (userExists)
                        {
                            context.sessionManager.SessionSetKey(ref context.session, "user", context.GetParam("login"));
                        }
                        else
                        {
                            // проверка на внутреннего пользователя
                            if (context.GetParam("login") == "test" && context.GetParam("password") == "1")
                            {
                                context.sessionManager.SessionSetKey(ref context.session, "user", "test");
                            }
                        }
                    }
                    catch { };
                }
            }

            return new ResponseContext("", "/");
        }


        // ----------------------------------------------------------------------------------------
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


        // ----------------------------------------------------------------------------------------
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
            TaggedProcess p = (TaggedProcess)context.sessionManager.SessionGetKey(context.session, "process");
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
                    p = new TaggedProcess(cmd);
                    
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

        // ----------------------------------------------------------------------------------------
        // Route: "/output"
        public static ResponseContext Output(RequestContext context)
        {
            // проверим доступ
            if (context.sessionManager.SessionGetKey(context.session, "user") == null)
            {
                // пользователь неавторизован - ничего не возвращаем
                return new ResponseContext("", "");
            }

            // глобальные переменные в сессии
            TaggedProcess p = (TaggedProcess)context.sessionManager.SessionGetKey(context.session, "process");
            if (p == null)
            {
                // процесс еще не запущен - ничего не возвращаем
                return new ResponseContext("");
            }

            if (context.Method == RequestMethod.POST)
            {
                // AJAX ЗАПРОС: ЗАПРОС ВЫВОДА
                int queueLen = p.outQueue.Count;
                if (queueLen > 0)
                {
                    List<string> arr = new List<string>();
                    string buf = "";
                    for (int i = 0; i < queueLen; i++)
                    {
                        if (p.outQueue.TryDequeue(out buf))
                            arr.Add(buf);
                    }

                    return new ResponseContext(TJson.ConvertToJson(arr));
                }
            }

            // если GET запрос - то перенаправим на главную страницу
            return new ResponseContext("", "/");
        }


        // ----------------------------------------------------------------------------------------
        // Route: "/input"
        public static ResponseContext Input(RequestContext context)
        {
            // проверим доступ
            if (context.sessionManager.SessionGetKey(context.session, "user") == null)
            {
                // пользователь неавторизован - ничего не возвращаем
                return new ResponseContext("");
            }

            // глобальные переменные в сессии
            TaggedProcess p = (TaggedProcess)context.sessionManager.SessionGetKey(context.session, "process");
            if (p == null)
            {
                // процесс еще не запущен - ничего не возвращаем
                return new ResponseContext("");
            }

            if (context.Method == RequestMethod.POST)
            {
                // AJAX ЗАПРОС: ЗАПРОС ВВОДА КОМАНДЫ
                p?.StandardInput.WriteLine((string)context.GetParam("cmd") + "\r\n");
            }

            return new ResponseContext("");
        }

        // --------------------------------------------------------------------
        /// <summary>
        /// Переопределенный класс от класса Process, чтобы включить очередь вывода STDOUT
        /// </summary>
        class TaggedProcess : Process
        {
            public ConcurrentQueue<string> outQueue;

            public TaggedProcess(string processName)
            {
                this.StartInfo.UseShellExecute = false;
                this.StartInfo.RedirectStandardOutput = true;
                this.StartInfo.RedirectStandardInput = true;
                this.StartInfo.RedirectStandardError = true;
                this.StartInfo.FileName = processName;
                this.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                this.StartInfo.CreateNoWindow = true;

                this.outQueue = new ConcurrentQueue<string>();

                this.OutputDataReceived += OutputDataReceived_Event;
                this.ErrorDataReceived += OutputDataReceived_Event;
            }

            private void OutputDataReceived_Event(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null)
                {
                    // строка возвращается в windows-1251
                    this.outQueue.Enqueue(e.Data + "\r\n");
                }
            }

            public void ClearOutputQueue()
            {
                while (this.outQueue.Count > 0)
                {
                    this.outQueue.TryDequeue(out string temp);
                }
            }
        }


        // ----------------------------------------------------------------------------------------
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
