using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Management;
using System.DirectoryServices.AccountManagement;

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
            Dictionary<string, object> data = new Dictionary<string, object>();
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

            data.Add("pcInfo", pcInfo);
            data.Add("date", DateTime.Now.ToString());
            data.Add("session", context.session.keys);

            TemplateParser tp = new TemplateParser(data);
            return new ResponseContext(tp.ParseFromResource("index.html", data));
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
    }
}
