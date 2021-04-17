using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Net;
using System.Web;
using System.Reflection;
using System.Resources;
using System.Linq;

namespace WebServer
{
    /// <summary>
    /// Класс реализующий функциональность веб-сервера
    /// </summary>
    class WebServerV1
    {
        /// <summary>
        /// управление сессиями
        /// </summary>
        private SessionManager sm = new SessionManager();

        /// <summary>
        /// веб-сервер
        /// </summary>
        private HttpListener webSrv = new HttpListener();

        /// <summary>
        /// Поток обработки входящих запросов. 
        /// При получении запроса, создает дочерний поток в который передает контекст запроса.
        /// </summary>
        Thread httpDispatcher;

        /// <summary>
        /// флаг остановки потока обработки web сервера
        /// </summary>
        private bool stopHttp = false;

        /// <summary>
        /// Имя кодовой страницы текста для преобразования ответа сервера клиенту.
        /// По умолчанию "UTF-8". Для кириллицы из кода Visual Studio должна быть "windows-1251".
        /// </summary>
        public string responseCodePage;

        /// <summary>
        /// Срок хранения сессий на сервере в минутах. По умолчанию 1 день.
        /// </summary>
        public double sessionDuration;

        /// <summary>
        /// Задает место расположения статического контента, в случае если он является внешним,
        /// т.е. находится в файлах. Поддерживает относительные пути. По умолчанию текущая директория.
        /// </summary>
        public string staticContent;

        /// <summary>
        /// Показывает откуда брать статические файлы. Если установлен в True, то на все запросы файлов (кроме
        /// HTML страниц, которые обрарабатываются в route-функциях, где напрямую укзавается метод
        /// парсинга в TemplateParser) будут искаться объекты в Embedded Resources. Если False,
        /// то файлы ищутся в директории staticContent. По умолчанию равен False.
        /// </summary>
        public bool useEmbeddedResources;

        /// <summary>
        /// Объявление определения указателя на функцию, которая будет выполнена, когда получен http запрос
        /// </summary>
        public delegate string RouteFunction(RequestContext context);

        /// <summary>
        /// Таблица переходов.
        /// </summary>
        private Dictionary<string, RouteFunction> routeTable = new Dictionary<string, RouteFunction>();

        /// <summary>
        /// Конструктор. Запускает веб сервер на прослушивание и обработку запросов.
        /// </summary>
        /// <param name="prefix">Префикс для прослушивания. По умолчанию "http://localhost:8080/".</param>
        public WebServerV1(string prefix = "http://localhost:8080/")
        {
            // зададим параметры
            this.responseCodePage = "UTF-8";
            this.sessionDuration = 24 * 60;
            this.staticContent = "";
            this.useEmbeddedResources = false;

            // запустим сервер
            this.stopHttp = false;
            this.webSrv.Prefixes.Add(prefix);
            this.webSrv.Start();

            // запустим поток обработки клиентских запросов
            this.httpDispatcher = new Thread(this.Listen);
            this.httpDispatcher.Priority = ThreadPriority.AboveNormal;
            this.httpDispatcher.IsBackground = true;
            this.httpDispatcher.Start();
        }

        /// <summary>
        /// Корректно завершает работу сервера.
        /// </summary>
        public void Stop()
        {
            this.stopHttp = true;
            this.webSrv.Stop();
            while (this.httpDispatcher.IsAlive);
        }

        /// <summary>
        /// Процедура получения и распределения http запросов.
        /// </summary>
        private void Listen()
        {
            while (!this.stopHttp)
            {
                try
                {
                    HttpListenerContext ctx = this.webSrv.GetContext();
                    new Thread(new ParameterizedThreadStart(ProcessRequest)).Start(ctx); // Priority=Normal
                }
                catch { };
            }
        }

        /// <summary>
        /// Обработка пользовательского запроса в отдельном потоке.
        /// </summary>
        /// <param name="context">Контекст запроса</param>
        void ProcessRequest(object context)
        {
            HttpListenerContext ctx = (HttpListenerContext)context;

            HttpListenerRequest request = ctx.Request;
            HttpListenerResponse response = ctx.Response;

            RequestContext rc = ParseRequest(request);

            // вначале проверяется не является ли запрошенный ресурс URL в таблице переходов
            if (this.routeTable.ContainsKey(rc.Route) && request.Headers["Accept"].Contains("text/html"))
            {
                // если да - запускаем парсер
                string responseString = ProcessRoute(rc);
                bool isSessionDelete = rc.sessionManager.LeaveSession(rc.session.sessionId);

                if (!isSessionDelete) response.AppendCookie(new Cookie("SSID", rc.session.sessionId));

                byte[] buffer = System.Text.Encoding.GetEncoding(this.responseCodePage).GetBytes(responseString);

                response.ContentType = "text/html";
                response.ContentLength64 = buffer.Length;
                Stream output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);

                output.Close();
            }
            else
            {
                // получим имя запрашиваемого файла с защитой от доступа к ресурсам вне разрешенной папки
                string file = rc.Route.Replace("..\\", string.Empty).Replace("../", string.Empty).TrimStart('\\', '/');
                string filename = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                                  this.staticContent,
                                  file);

                
                string extension = Path.GetExtension(filename);
                string embeddedResourceName = $"{Assembly.GetExecutingAssembly().GetName().Name}.Resources.{file}";

                // проверим что запрашиваемый ресурс в списке доступных
                if (allowedMimeTypes.Keys.Contains(extension))
                {
                    try
                    {
                        if (!this.useEmbeddedResources && File.Exists(filename))
                        {
                            // ищем файл на сервере в доступных для этого папках и передаем
                            Stream input = new FileStream(filename, FileMode.Open);
                            response.ContentType = allowedMimeTypes[extension];
                            response.ContentLength64 = input.Length;
                            input.CopyTo(response.OutputStream);
                            input.Close();
                            response.OutputStream.Close();
                        }
                        else if (this.useEmbeddedResources &&
                                (Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(x => x == embeddedResourceName).Count() > 0))
                        {
                            // ищем файл среди встроенные ресурсов
                            Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream("WebServer.Resources.http_status_not_found_icon.png");
                            response.ContentType = allowedMimeTypes[extension];
                            input.CopyTo(response.OutputStream);
                            input.Close();
                        }
                        else
                        {
                            response.StatusCode = (int)HttpStatusCode.NotFound; // вначале всегда код возврата
                            response.ContentLength64 = 0;
                            response.OutputStream.Close(); // а в самом конце закрытие потока
                        }
                    }
                    catch
                    {
                        response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        response.ContentLength64 = 0;
                        response.OutputStream.Close();
                    }
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.Forbidden;
                    response.ContentLength64 = 0;
                    response.OutputStream.Close();
                }
            }      
        }

        /// <summary>
        /// Парсинг запроса.
        /// </summary>
        /// <param name="request">Объект контекста запроса.</param>
        /// <returns>Класс RequestContext.</returns>
        private RequestContext ParseRequest(HttpListenerRequest request)
        {
            RequestContext rc = new RequestContext
            {
                values = new Dictionary<string, object>(),
                Method = (RequestMethod)Enum.Parse(typeof(RequestMethod), request.HttpMethod.ToString().ToUpper()),
                encoding = request.ContentEncoding,
                Route = request.RawUrl.Split('?')[0],
                sessionManager = this.sm,
                session = sm.GetSession(request.Cookies["SSID"]?.Value),
                baseRequest = request
            };

            if (rc.session == null) rc.session = sm.CreateSession(this.sessionDuration);

            if (rc.Method == RequestMethod.GET)
            {
                foreach (string key in request.QueryString.AllKeys)
                {
                    rc.values.Add(key, request.QueryString[key]);
                }
            }
            else if (rc.Method == RequestMethod.POST)
            {
                //"application/x-www-form-urlencoded"
                System.IO.StreamReader reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding);
                string s = reader.ReadToEnd();
                if (s != "")
                {
                    string[] values = s.Split('&');
                    foreach (string value in values)
                    {
                        string[] pair = value.Split('=');
                        rc.values.Add(pair[0], HttpUtility.UrlDecode(pair[1]));
                    }
                }
                request.InputStream.Close();
                reader.Close();
            }

            return rc;
        }

        /// <summary>
        /// Добавляет маршрут к таблице маршрутов.
        /// </summary>
        /// <param name="route">Адрес перехода начинающийся с правого слеша "/".</param>
        /// <param name="function">Функция для выполнения.</param>
        public void AddRoute(string route, RouteFunction function)
        {
            this.routeTable.Add(route, function);
        }

        /// <summary>
        /// Ищет заданный маршрут в таблице маршрутов и выполняет соответствующую функцию.
        /// </summary>
        /// <param name="context">Контест запроса.</param>
        /// <returns>HTML код который должен быть возвращен пользователю.</returns>
        private string ProcessRoute(RequestContext context)
        {
            try
            {
                return this.routeTable[context.Route]?.Invoke(context);
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Словарь содержащий разрешенные типы запрашиваемых ресурсов
        /// </summary>
        private static IDictionary<string, string> allowedMimeTypes =
            new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    {".css", "text/css"},
                    {".gif", "image/gif"},
                    {".ico", "image/x-icon"},
                    {".jpeg", "image/jpeg"},
                    {".jpg", "image/jpeg"},
                    {".js", "application/x-javascript"},
                    {".png", "image/png"}
                };
        /*
                    {".asf", "video/x-ms-asf"},
                    {".asx", "video/x-ms-asf"},
                    {".avi", "video/x-msvideo"},
                    {".bin", "application/octet-stream"},
                    {".cco", "application/x-cocoa"},
                    {".crt", "application/x-x509-ca-cert"},
                    {".css", "text/css"},
                    {".deb", "application/octet-stream"},
                    {".der", "application/x-x509-ca-cert"},
                    {".dll", "application/octet-stream"},
                    {".dmg", "application/octet-stream"},
                    {".ear", "application/java-archive"},
                    {".eot", "application/octet-stream"},
                    {".exe", "application/octet-stream"},
                    {".flv", "video/x-flv"},
                    {".gif", "image/gif"},
                    {".hqx", "application/mac-binhex40"},
                    {".htc", "text/x-component"},
                    {".htm", "text/html"},
                    {".html", "text/html"},
                    {".ico", "image/x-icon"},
                    {".img", "application/octet-stream"},
                    {".iso", "application/octet-stream"},
                    {".jar", "application/java-archive"},
                    {".jardiff", "application/x-java-archive-diff"},
                    {".jng", "image/x-jng"},
                    {".jnlp", "application/x-java-jnlp-file"},
                    {".jpeg", "image/jpeg"},
                    {".jpg", "image/jpeg"},
                    {".js", "application/x-javascript"},
                    {".mml", "text/mathml"},
                    {".mng", "video/x-mng"},
                    {".mov", "video/quicktime"},
                    {".mp3", "audio/mpeg"},
                    {".mpeg", "video/mpeg"},
                    {".mpg", "video/mpeg"},
                    {".msi", "application/octet-stream"},
                    {".msm", "application/octet-stream"},
                    {".msp", "application/octet-stream"},
                    {".pdb", "application/x-pilot"},
                    {".pdf", "application/pdf"},
                    {".pem", "application/x-x509-ca-cert"},
                    {".pl", "application/x-perl"},
                    {".pm", "application/x-perl"},
                    {".png", "image/png"},
                    {".prc", "application/x-pilot"},
                    {".ra", "audio/x-realaudio"},
                    {".rar", "application/x-rar-compressed"},
                    {".rpm", "application/x-redhat-package-manager"},
                    {".rss", "text/xml"},
                    {".run", "application/x-makeself"},
                    {".sea", "application/x-sea"},
                    {".shtml", "text/html"},
                    {".sit", "application/x-stuffit"},
                    {".swf", "application/x-shockwave-flash"},
                    {".tcl", "application/x-tcl"},
                    {".tk", "application/x-tcl"},
                    {".txt", "text/plain"},
                    {".war", "application/java-archive"},
                    {".wbmp", "image/vnd.wap.wbmp"},
                    {".wmv", "video/x-ms-wmv"},
                    {".xml", "text/xml"},
                    {".xpi", "application/x-xpinstall"},
                    {".zip", "application/zip"},
         */
    }



    /// <summary>
    /// Определение стркутуры пользовательского контеста для передачи в функцию RouteFunction.
    /// </summary>
    class RequestContext
    {
        // основные параметры
        public RequestMethod Method; // метод: GET, POST...
        public string Route; // запрошенный URL начинающийся с "/"
        public Encoding encoding; // кодировка страницы
        public Dictionary<string, object> values; // словарь параметров запроса
        public SessionData session; // ссылка на объект пользовательской 
        // расширенные параметры
        public HttpListenerRequest baseRequest; // ссылка на базовый объект запроса
        public SessionManager sessionManager; // ссылка на менеджер сессий
    }

    /// <summary>
    /// Перечисление обрабатываемых методов http
    /// </summary>
    enum RequestMethod
    {
        GET,
        POST,
        OPTIONS,
        HEAD,
        PUT,
        PATCH,
        DELETE,
        TRACE,
        CONNECT
    }

    

}
