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
        public delegate ResponseContext RouteFunction(RequestContext context);

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
            if (this.routeTable.ContainsKey(rc.Route) && 
                 (request.Headers["Accept"].Contains("text/html") || request.Headers["Accept"].Contains("*/*")))
            {
                // если да - запускаем парсер и возвращаем его ответ
                ResponseContext userResponse = ProcessRoute(rc);

                if (userResponse.redirectUrl != "")
                {
                    // пользовательская функция вызвала метод перенаправления (redirect)
                    if (rc.session != null)
                        response.AppendCookie(new Cookie("SSID", rc.session.sessionId));
                    else
                    {
                        if (request.Cookies["SSID"] != null)
                        {
                            // удалить сессионные cookie
                            response.AppendCookie(request.Cookies["SSID"]);
                            response.Cookies["SSID"].Expired = true;
                            response.Cookies["SSID"].Discard = true;
                        }
                    }

                    response.Redirect(userResponse.redirectUrl);
                    response.OutputStream.Close();
                }
                else
                {
                    // вернем в браузер ответ пользовательской функции

                    // установим или удалим сессионные cookie
                    if (rc.session != null)
                        response.AppendCookie(new Cookie("SSID", rc.session.sessionId));
                    else
                    {
                        if (request.Cookies["SSID"] != null)
                        {
                            // удалить сессионные cookie
                            response.AppendCookie(request.Cookies["SSID"]);
                            response.Cookies["SSID"].Expired = true;
                            response.Cookies["SSID"].Discard = true;
                        }
                    }

                    byte[] buffer = System.Text.Encoding.GetEncoding(this.responseCodePage).GetBytes(userResponse.responseString);

                    response.StatusCode = (int)userResponse.exitCode;
                    response.ContentType = "text/html";
                    response.ContentLength64 = buffer.Length;
                    Stream output = response.OutputStream;
                    output.Write(buffer, 0, buffer.Length);

                    output.Close();
                }
            }
            else
            {
                // --- запрошенный ресурс не в таблице маршрутизации - это или ресурс или ошибочная ссылка

                if (!this.useEmbeddedResources)
                {
                    // если сервер использует режим хранения ресурсов в файловой системе

                    // получим имя запрашиваемого файла с защитой от доступа к ресурсам вне разрешенной папки
                    string file = rc.Route.Replace("..\\", string.Empty).Replace("../", string.Empty).TrimStart('\\', '/');
                    string filename = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                                      this.staticContent,
                                      file);
                    string extension = Path.GetExtension(filename);

                    // проверим что запрашиваемый ресурс в списке разрешенных
                    if (allowedMimeTypes.Keys.Contains(extension))
                    {
                        if (File.Exists(filename))
                        {
                            // ищем файл на сервере в доступных для этого папках и передаем
                            Stream input = new FileStream(filename, FileMode.Open);
                            response.ContentType = allowedMimeTypes[extension];
                            response.ContentLength64 = input.Length;
                            this.CopyStream(input, response.OutputStream);
                            input.Close();
                            response.OutputStream.Close();
                        }
                        else
                        {
                            // файл не найден
                            response.StatusCode = (int)HttpStatusCode.NotFound;
                            response.ContentLength64 = 0;
                            response.OutputStream.Close();
                        }
                    }
                    else
                    {
                        // запрошен неразрешенный тип файла
                        response.StatusCode = (int)HttpStatusCode.Forbidden;
                        response.ContentLength64 = 0;
                        response.OutputStream.Close();
                    }
                }
                else
                {
                    // если сервер использует режим хранения ресурсов в сборке EXE
                    string embeddedResourceName = $"{Assembly.GetExecutingAssembly().GetName().Name}.Resources.{rc.Route.TrimStart('\\', '/')}";
                    string extension = Path.GetExtension(rc.Route);

                    // проверим что запрашиваемый ресурс в списке разрешенных
                    if (allowedMimeTypes.Keys.Contains(extension))
                    {
                        // ищем запрошенный файл в ресурсах и передаем
                        if ((Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(x => x == embeddedResourceName).Count() > 0))
                        {
                            Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream(embeddedResourceName);
                            response.ContentType = allowedMimeTypes[extension];
                            response.ContentLength64 = input.Length;
                            this.CopyStream(input, response.OutputStream);
                            input.Close();
                            response.OutputStream.Close();
                        }
                        else
                        {
                            // файл не найден
                            response.StatusCode = (int)HttpStatusCode.NotFound;
                            response.ContentLength64 = 0;
                            response.OutputStream.Close();
                        }
                    }
                    else
                    {
                        // запрошен неразрешенный тип файла
                        response.StatusCode = (int)HttpStatusCode.Forbidden; // вначале всегда код возврата
                        response.ContentLength64 = 0;
                        response.OutputStream.Close(); // а в самом конце закрытие потока
                    }
                }
            }      
        }

        /// <summary>
        /// Копирует поток в поток асинхронно. Это правильная замена встроенной функции srcStream.CopyTo(destStream),
        /// т.к. последняя имеет баг и выполняется асинхронно.
        /// </summary>
        /// <param name="input">Входной поток (из которого копируется).</param>
        /// <param name="output">Выходной поток (в который копируется)</param>
        /// <returns>Количество скопированных байтов.</returns>
        private long CopyStream(Stream input, Stream output)
        {
            long copiesBytes = 0;
            byte[] buffer = new byte[32768];
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
                copiesBytes += read;
            }
            return copiesBytes;
        }

        /// <summary>
        /// Разбирает запрос и создает контекст запроса. В контест передаются данные о методе запроса,
        /// переменных, данные о сессии, маршрут и т.п.
        /// </summary>
        /// <param name="request">Объект контекста запроса.</param>
        /// <returns>Класс RequestContext.</returns>
        private RequestContext ParseRequest(HttpListenerRequest request)
        {
            RequestContext rc = new RequestContext
            {
                templateVariables = new Dictionary<string, object>(),
                parameters = new Dictionary<string, object>(),
                Method = (RequestMethod)Enum.Parse(typeof(RequestMethod), request.HttpMethod.ToString().ToUpper()),
                Route = request.RawUrl.Split('?')[0],
                sessionManager = this.sm,
                session = sm.GetSession(request.Cookies["SSID"]?.Value),
                baseRequest = request,
                redirect = ""
            };

            rc.templateVariables.Add("session", rc.session?.keys);

            if (rc.Method == RequestMethod.GET)
            {
                foreach (string key in request.QueryString.AllKeys)
                {
                    rc.parameters.Add(key, request.QueryString[key]);
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
                        rc.parameters.Add(pair[0], HttpUtility.UrlDecode(pair[1]));
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
        private ResponseContext ProcessRoute(RequestContext context)
        {
            try
            {
                return this.routeTable[context.Route]?.Invoke(context);
            }
            catch
            {
                return new ResponseContext("Internal server error", "", HttpStatusCode.InternalServerError);
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
                    {".map", "text/css"},
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
        // ---- основные параметры

        /// <summary>
        /// Метод запроса (GET, POST...)
        /// </summary>
        public RequestMethod Method;

        /// <summary>
        /// Запрошенный URL начинающийся с "/"
        /// </summary>
        public string Route;

        /// <summary>
        /// Словарь параметров запроса
        /// </summary>
        public Dictionary<string, object> parameters;

        /// <summary>
        /// Словарь переменных для класса TemplateParser.
        /// Создан для удобства, что бы не создавать пользователем.
        /// </summary>
        public Dictionary<string, object> templateVariables;

        /// <summary>
        /// Прямая ссылка на объект пользовательской сессии
        /// </summary>
        public SessionData session;

        // ---- расширенные параметры
        public HttpListenerRequest baseRequest; // ссылка на базовый объект запроса
        public SessionManager sessionManager; // ссылка на менеджер сессий
        public string redirect; // если установлен, то ссылка перехода

        // ---- методы

        /// <summary>
        /// Возвращает значение параметра полученного через GET/POST. Данный метод более
        /// предпочтительней, чем прямой доступ к словарю values.
        /// </summary>
        /// <param name="name">Имя параметра.</param>
        /// <param name="defaultValue">Значене по умолчанию, если параметра нет в списке.
        /// По умолчанию возвращается пустая строка.</param>
        /// <returns>Значение параметра или пустая строка, если параметр не задан.</returns>
        public string GetParam(string name, string defaultValue = "")
        {
            return (parameters.ContainsKey(name) ? parameters[name].ToString() : defaultValue);
        }

        /// <summary>
        /// Возвращает количество параметров в запросе.
        /// </summary>
        /// <returns>Количество параметров в запросе</returns>
        public int GetParamsCount()
        {
            return parameters.Count;
        }
    }

    /// <summary>
    /// Определение структуры возвращаемого контекста после обработки пользовательской функцией
    /// </summary>
    class ResponseContext
    {
        /// <summary>
        /// Обработанный HTML ответ.
        /// </summary>
        public string responseString;

        /// <summary>
        /// Если не равен пустой строке, то требуется переход.
        /// </summary>
        public string redirectUrl;

        /// <summary>
        /// Код выхода, по умолчанию "OK"(200)
        /// </summary>
        public HttpStatusCode exitCode;

        /// <summary>
        /// Конструктор. Создает возвращаемый класс обработки запроса пользовательской функцией.
        /// </summary>
        /// <param name="responseString">Возвращаемый текст HTML.</param>
        /// <param name="redirectUrl">Адрес перехода. Если установлен, то будет отравлен код 302 с адресом,
        /// а ResponseString проигнорирована.</param>
        public ResponseContext(string responseString = "", string redirectUrl = "", HttpStatusCode exitCode = HttpStatusCode.OK)
        {
            this.redirectUrl = redirectUrl;
            this.responseString = responseString;
            this.exitCode = exitCode;
        }
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
