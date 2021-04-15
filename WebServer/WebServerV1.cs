using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Web;

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
        /// <param name="port">Порт, на котором будем слушать запросы. По умолчанию равен "8080".</param>
        public WebServerV1(string port="8080")
        {
            // зададим параметры
            responseCodePage = "UTF-8";
            sessionDuration = 24 * 60;

            // запустим сервер
            stopHttp = false;
            webSrv.Prefixes.Add($"http://localhost:{port}/");
            webSrv.Start();

            // запустим поток обработки клиентских запросов
            httpDispatcher = new Thread(Listen);
            httpDispatcher.Priority = ThreadPriority.AboveNormal;
            httpDispatcher.IsBackground = true;
            httpDispatcher.Start();
        }

        /// <summary>
        /// Корректно завершает работу сервера.
        /// </summary>
        public void Stop()
        {
            stopHttp = true;
            webSrv.Stop();
            while (httpDispatcher.IsAlive);
        }

        /// <summary>
        /// Процедура получения и распределения http запросов.
        /// </summary>
        private void Listen()
        {
            while (!stopHttp)
            {
                try
                {
                    HttpListenerContext ctx = webSrv.GetContext();
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
            string responseString = ProcessRoute(rc);
            bool isSessionDelete = rc.sessionManager.LeaveSession(rc.session.sessionId);

            if (!isSessionDelete) response.AppendCookie(new Cookie("SSID", rc.session.sessionId));

            byte[] buffer = System.Text.Encoding.GetEncoding(this.responseCodePage).GetBytes(responseString);

            response.ContentLength64 = buffer.Length;
            System.IO.Stream output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);

            output.Close();
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
            routeTable.Add(route, function);
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
                return routeTable[context.Route]?.Invoke(context);
            }
            catch
            {
                return "";
            }
        }
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

    enum RequestMethod
    {
        GET,
        POST
    }
}
