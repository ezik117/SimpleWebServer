using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Web.SessionState;


namespace WebServer
{
    /// <summary>
    /// Класс управления объектами сессии
    /// </summary>
    public class SessionManager
    {
        /// <summary>
        /// Список сессий. Ключ=ID сессии. Значение=класс SessionData.
        /// </summary>
        private ConcurrentDictionary<string, SessionData> sessionData;

        private Thread cleanUpThread;

        /// <summary>
        /// Конструктор. Инициализирует работу Менеджера сессий.
        /// </summary>
        public SessionManager()
        {
            sessionData = new ConcurrentDictionary<string, SessionData>();

            cleanUpThread = new Thread(_CleanUp);
            cleanUpThread.Priority = ThreadPriority.Lowest;
            cleanUpThread.IsBackground = true;
            cleanUpThread.Start();
        }

        /// <summary>
        /// Создает сессию с указанным временем жизни.
        /// </summary>
        /// <param name="expiredMinutes">Время жизни сессии в минутах. Значение по умолчанию=0, что означет 
        /// что объект никогда не удаляется.</param>
        /// <param name="sessionID">ID сессии. По умолчанию равно null, т.е. генерируется новый ID.</param>
        /// <returns>Возвращает ссылку на класс SessionData.</returns>
        public SessionData CreateSession(double expiredMinutes = 0, string sessionID = null)
        {
            SessionData sd = new SessionData(expiredMinutes, sessionID);
            sd.SetInUse();
            bool res = sessionData.TryAdd(sd.sessionId, sd);
            if (!res) throw new Exception("The sessin cannot be created");
            return sd;
        }

        /// <summary>
        /// Возвращает класс SessionData содержащий все данные о сессии.
        /// Если сессия просрочена, возращает null.
        /// </summary>
        /// <param name="sessionId">ID сессии.</param>
        /// <returns>Возвращает ссылку на класс SessionData или null.</returns>
        public SessionData GetSession(string sessionId)
        {
            if (sessionId == null) return null;

            try
            {
                SessionData sd = this.sessionData[sessionId];
                if (sd.IsExpired()) return null;
                sd.SetInUse();
                return sd;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Покинуть сессиию. Рекомендуется вызывать данный метод при выходе из контекста пользователя. 
        /// Метод проверяет, есть ли у сессии установленные ключи. Если ключей нет, то сессия удаляется.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns>True, если сессия была удалена, т.к. не содержит ключей, иначе False</returns>
        public bool LeaveSession(string sessionId)
        {
            try
            {
                if (sessionData[sessionId].keys.Count == 0)
                {
                    DeleteSession(sessionId);
                    return true;
                }
            }
            catch
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Удаляет указанную сессию.
        /// </summary>
        /// <param name="sessionId">ID сессии.</param>
        /// <returns>True, если удалено успешно, иначе False.</returns>
        public bool DeleteSession(string sessionId)
        {
            return sessionData.TryRemove(sessionId, out SessionData temp);
        }

        public void SessionSetKey(ref SessionData session, string key, object value)
        {
            if (session == null) session = this.CreateSession();
            session.Set(key, value);
        }

        public object SessionGetKey(SessionData session, string key, object defaultValue = null)
        {
            if (session == null) return defaultValue;

            return session.Get(key, defaultValue);
        }

        public void SessionDeleteKey(ref SessionData session, string key)
        {
            if (session == null) return;

            if (session.keys.ContainsKey(key)) session.keys.Remove(key);

            if (session.keys.Count == 0)
            {
                this.DeleteSession(session.sessionId);
                session = null;
            }
        }

        public void SessionClear(ref SessionData session)
        {
            if (session == null) return;
            this.DeleteSession(session.sessionId);
            session = null;
        }

        /// <summary>
        /// Бесконечный цикл потока производящего автоматическую очистку сессий.
        /// Удаляет все сессии старее, чем значение bestBefore.
        /// Очистка происходит каждые 5 минут.
        /// </summary>
        private void _CleanUp()
        {
            while (true)
            {
                try
                {
                    DateTime now = DateTime.Now;
                    List<string> idToBeRemoved = new List<string>();

                    foreach (KeyValuePair<string, SessionData> sd in sessionData)
                    {
                        if (sd.Value.IsExpired() && !sd.Value.inUse)
                        {
                            idToBeRemoved.Add(sd.Value.sessionId);
                        }
                    }

                    foreach (string id in idToBeRemoved)
                    {
                        sessionData.TryRemove(id, out SessionData temp);
                    }
                }
                catch { };

                Thread.Sleep(TimeSpan.FromMinutes(5));
            }
        }
    }



    /// <summary>
    /// Класс описывающий один объект сессии.
    /// </summary>
    public class SessionData
    {
        /// <summary>
        /// Конструктор. Создает объект сессии с указанным временем жизни.
        /// Объект сессии может быть с новым session ID или с заданным.
        /// </summary>
        /// <param name="expiredMinutes">Время жизни объекта в минутах.</param>
        /// <param name="sessionID">ID сессии. По умолчанию равно null, т.е. генерируется новый ID.</param>
        public SessionData(double expiredMinutes, string sessionID = null)
        {
            if (sessionID == null)
                this.sessionId = ((SessionIDManager)(new SessionIDManager())).CreateSessionID(null);
            else
                this.sessionId = sessionID;

            this.keys = new Dictionary<string, object>();
            this.expiration = expiredMinutes;
        }

        /// <summary>
        /// ID сессии.
        /// </summary>
        public string sessionId;

        /// <summary>
        /// Хранимые значения в сессии.
        /// </summary>
        public Dictionary<string, object> keys;

        /// <summary>
        /// Время жизни сессии в минутах. Установка этого параметра автоматически продлит время сессии на указанное значение.
        /// Если значение равно 0, то сессия считается бесконечной.
        /// </summary>
        public double expiration
        {
            get { return _expiration; }
            set
            {
                this._bestBefore = DateTime.Now.AddMinutes(value);
                this._expiration = value;
            }
        }
        public double _expiration;

        /// <summary>
        /// Время, после которого сессия будет уничтожена.
        /// </summary>
        private DateTime _bestBefore;

        /// <summary>
        /// Индикатор блокировки сессии от удаления Менеджером сессий при очистке.
        /// Блокировка вызывается автоматически при создании сессии или при запросе объекта сессии.
        /// Вручную вызывается методом setInUse().
        /// Объект сессии блокируется на 30 секунд. Если в течение этого времени будет вызван метод
        /// cleanUp() Менеджера сессий, то объект сессии не будет удален, даже если он уже просрочен.
        /// Только для чтения.
        /// </summary>
        public bool inUse
        {
            get
            {
                return this._inUse > DateTime.Now;
            }
        }
        private DateTime _inUse;

        /// <summary>
        /// Возвращает значение ключевой пары преобразуя к типу string. Если ключевая пара не существует
        /// возвращает значение defaultValue.
        /// </summary>
        /// <param name="key">Имя ключа.</param>
        /// <param name="defaultValue">Возвращаемое значение, если ключ не найден. По умолчанию null.</param>
        /// <returns>Строковое значение ключевой пары.</returns>
        public string GetString(string key, string defaultValue = null)
        {
            try
            {
                return this.keys[key].ToString();
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Возвращает значение ключевой пары. Если ключевая пара не существует
        /// возвращает значение defaultValue.
        /// </summary>
        /// <param name="key">Имя ключа.</param>
        /// <param name="defaultValue">Возвращаемое значение, если ключ не найден. По умолчанию null.</param>
        /// <returns>Строковое значение ключевой пары.</returns>
        public object Get(string key, object defaultValue = null)
        {
            try
            {
                return this.keys[key];
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Задает значение ключа сессии.
        /// </summary>
        /// <param name="key">Ключ.</param>
        /// <param name="value">Значение.</param>
        public void Set(string key, object value)
        {
            if (!this.keys.ContainsKey(key))
                this.keys.Add(key, value);
            else
                this.keys[key] = value;
        }

        /// <summary>
        /// Возвращает проверку на истечение времени жизни сессии.
        /// Так же проверяется значение expiration, если оно равно 0, то сессия никогда не устаревает.
        /// </summary>
        /// <returns>True-если сессия устарела, иначе False.</returns>
        public bool IsExpired()
        {
            if (this._expiration == 0) return false;
            return (this._bestBefore < DateTime.Now);
        }

        /// <summary>
        /// Устанавливает время блокирования объекта сессии от удаления на 30 секунд.
        /// Подробнее смотри описание переменной _inUse.
        /// </summary>
        public void SetInUse()
        {
            this._inUse = DateTime.Now.AddSeconds(30);
        }

        /// <summary>
        /// Продлевает время жизни сессии на значение установленное в свойстве expiration.
        /// </summary>
        public void ProlongExpirtaion()
        {
            this.expiration = this._expiration;
        }
    }
}

/*
 Логика:
 - если получен запрос из него выделяется ID сессии, если есть иначе null
 - на выходе из обработки HTTP запроса проверяется, есть ли в словаре данных сессии хоть один ключ
 - если ключей нет, то куки "SSID" не создаются, вызывается метод 
 - если ключи есть, то передаются куки "SSID" с ID сессии

    Взаимодействие с сессиями напрямую не рекомендуется. Только через методы менджера сессий.

    Сессия создается, когда пользователь создает первый ключ в сессии. SessionSetKey(session, key, value)
    Сессия удаляется, когда удаляется последний ключ в сессии. SessionDeleteKey(session, key)
    Сессия удаляется, когда пользователь удаляет все ключи сессии. SessionClear(session)
    Получить значение ключа сессии, SessionGetKey(session, key, defaultValue) или напрямую из объекта сессии session

    В контексте запроса пользователю передается ссылка на Менеджер сессий и на объект текущей сессии.

     
     */