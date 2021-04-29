# МНОГОПОТОЧНЫЙ ВЕБ-СЕРВЕР С ШАБЛОНИЗАТОРОМ И ПОДДЕРЖКОЙ СЕССИЙ

## ВВЕДЕНИЕ

Простой многопоточный сервер с поддержкой пользовательских сессий и встроенным шаблонизатором по принципу Jinja для использования в проектах C#, где необходим веб-интерфейс.

Работа сервера строится на настройке двух классов:

- WebServerV1
- RouteFunctions

**WebServerV1** - класс содержащий все необходимые действия по многопоточной обработке запросов на базе таблицы маршрутизации и обслуживанию сессий.

**RouteFunctions** - статический класс содержащий методы которые будет вызывать таблица маршрутизации.

## КЛАСС 'WebServerV1'

Основной класс для запуска веб сервера. По умолчанию обрабатывает входящие локальные запросы на 8080 порту (префикс "http://localhost:8080").

В случае использования в качестве удаленного сервера принимающего запросы на любом интерфейсе необходимо следующее:

- инициализировать сервер с параметром `prefix="http://+:8080/"`
- добавить разрешение через командную строку: `netsh http add urlacl url=http://+:8080/`

Примечание: просмотр текущих сетевых разрешений: `netsh http show urlacl`. Удаление разрешения: `netsh http delete urlacl url=http://+:8080/`

### СВОЙСТВА

Имя свойства | Тип | Описание
------------ | --- | --------
responseCodePage | string | Имя кодовой страницы в котором хранится текст шаблона для ответа сервера клиенту. По умолчанию "UTF-8". Для кириллицы из кода Visual Studio должна быть "windows-1251".
staticContent | string | Задает место расположения статического контента, в случае если он является внешним, т.е. картинки, файлы css и т.п хранятся в файлах, а не являются встроенным ресурсом (EmbeddedResource). Поддерживает относительные пути вида ("..\\..\\"). Значение по умолчанию - текущая директория.
useEmbeddedResources | bool | Показывает откуда брать статические файлы. Если установлен в True, то на все запросы файлов (кроме HTML страниц, которые обрарабатываются в route-функциях, где напрямую указывается путь к шаблонам) объекты будут искаться в Embedded Resources. Если False, то файлы ищутся в директории staticContent. По умолчанию равен False.

### МЕТОДЫ

Полный код объявления | Описание
--------------------- | --------
void WebServerV1(string prefix = "http://localhost:8080/") | Конструктор. Запускает веб сервер на прослушивание и обработку запросов. Префикс является префиксом класса HttpListener.
void Stop() | Корректно завершает работу сервера. Можно завершать программу без вызова данного метода.
public void AddRoute(string route, RouteFunction function) | Добавляет маршрут перехода в таблицу маршрутов.

## КЛАСС 'RouteFunctions'

Статический класс содержащий статические методы выполняемые, если запрошенный адрес совпадает с указанным маршрутом в таблице маршрутизации класса **WebServerV1**. Класс и его методы могут быть динамическими, но тогда он должен быть инициализирован до добавления методов в таблицу маршрутизации.

Все методы класса должны соответствовать определению делегата `public delegate ResponseContext RouteFunction(RequestContext context)` класса **WebServerV1**.

### КОНТЕКСТ ЗАПРОСА

В метод передается переменная **context** класса **SessionData** содержащая контекст запроса. Ниже даны свойства и методы данной переменной:

Имя свойства | Тип | Описание
------------ | --- | --------
Method | RequestMethod | Перечисление. Содержит метод HTTP запроса.
Route | string | Запрошенный URL начинающийся с "/". Например: "/" = "http://localhost:8080", "/login" = "http://localhost:8080/login"
parameters | Dictionary<string, string> | Словарные пары HTTP-запроса.
templateVariables | Dictionary<string, object> | Предварительно созданный словарь для HTML-шаблона. По умолчанию содержит объект **SessionData** с именем 'session'. Можно использовать свои словари для передачи в шаблонизатор **TemplateParser**.
session | SessionData | Прямая ссылка на объект пользовательской сессии типа **SessionData**. Не рекомендуется испоьзовать напрямую. Для управления сессиями необходимо использовать методы класса **SessionManager**.
sessionManager | SessionManager | Ссылка на класс **SessionManager** для управления пользовательской сессией.
baseRequest | HttpListenerRequest | Ссылка на исходный класс **HttpListenerRequest** для получения дополнительных параметров запроса.

Полный код объявления | Описание
--------------------- | --------
public string GetParam(string name, string defaultValue = "") | Возвращает значение параметра полученного через GET/POST. Данный метод более предпочтительней, чем прямой доступ к словарю values.
public int GetParamsCount() | Возвращает количество HTTP-параметров в запросе.

### КОНТЕСТ ОТВЕТА

Пользовательская функция должна вернуть объект класса **ResponseContext** объявленный как `public ResponseContext(string responseString = "", string redirectUrl = "", HttpStatusCode exitCode = HttpStatusCode.OK)`, где:

- **responseString** - строка HTML для ответа клиенту. Может быть как в виде простого текста (например для ответов в формате XML или JSON), так и ввиде предварительно обработанного шаблона через шаблонизатор **TemplateParser**.
- **redirectUrl** - строка перехода (redirect). По умолчанию равна пустой строке и может быть опущена при ответе. Есил не равна пустой строке, то вызывает ответ сервера говорящий клиенту о том, что ресурс перемещен и необходимо перейти на другую страницу. Данный редирект использует клиента для перенаправления на другую страницу или web-ресурс. Если необходимо использовать выполнение другой пользовательской функции, то ее необходимо вызвать напрямую из кода. При этом не стоит забывать, что если в другую пользовательскую функцию будет передан объект запроса из оригинальной функции, то он будет содержать все параметры текущего запроса. В случае изменения параметров их неохобходимо вручную отредактировать через свойство **parameters** объекта **SessionData**. 
- **exitCode** - код ответа сервера клиенту. По умолчанию 200 - "ОК". Может быть опущен при ответе.

## ПРИМЕРЫ

### Создание сервера и базовой страницы

```C#
static void Main(string[] args)
    {
        WebServerV1 www = new WebServerV1();
        www.AddRoute("/", RouteFunctions.Index);

        Console.WriteLine("Press a key to exit");
        Console.ReadKey(true);

        www.Stop();
    }

...

static class RouteFunctions
{
    // Route: "/"
    public static ResponseContext Index(RequestContext context)
    {
        context.variables.Add("dateNow", DateTime.Now);

        TemplateParser tp = new TemplateParser();
        
        return new ResponseContext(tp.ParseFromString(@"<HTML><BODY>Today is {{ dateNow }}</BODY></HTML>", context.variables));
    }
}

```

### Получение данных из web-страницы

```HTML
<form method="POST" action="/person">
  <input name="username">
  <button type="submit">
</form>

<form method="GET" action="/person">
  <input name="gender">
  <button type="submit">
</form>
```

```C#
static class RouteFunctions
{
    // Route: "/person"
    public static ResponseContext Person(RequestContext context)
    {
        string login;
        string gender;

        if (context.Method == RequestMethod.POST)
        {
            if ((login = context.GetParam("login")) != "")
            {
                // some code here
            }
        }

        if (context.Method == RequestMethod.GET)
        {
            if ((gender = context.GetParam("gender")) != "")
            {
                // some code here
            }
        }
    }
}
```

### Использование пользовательской сессии

```C#
// Route: "/logon"
public static ResponseContext Logon(RequestContext context)
{
    if (context.GetParam("login") == "test" && context.GetParam("password") == "1")
    {
        // сессия создается автоматически, как только есть хоть один ключ
        // по умолчанию создается сеансная сессия
        context.sessionManager.SessionSetKey(ref context.session, "user", "test");

        // можно вручную установить время сессии. здесь - 24 часа.
        context.session.expiration = 60*24;
    }
}

// Route: "/logout"
public static ResponseContext Logout(RequestContext context)
{
    context.sessionManager.SessionClear(ref context.session);
}

// Route: "/page1"
public static ResponseContext Page1(RequestContext context)
{
    // проверим доступ
    if (context.sessionManager.SessionGetKey(context.session, "user") == null)
    {
        // пользователь неавторизован - редирект на главную страницу
        return new ResponseContext("", "/");
    }

    // в сессии можно хранить любые объекты
    List<int> list = context.sessionManager.SessionGetKey(context.session, "list", new List<int>());
    list.Add(10);
    context.sessionManager.SessionSetKey(ref context.session, "list", list);
}
```

### Редирект страниц и шаблоны ошибок http

```C#
// данная строка возвратит пустой ответ браузеру клиента с перенаправлением в Header на страницу "newpage" 
return new ResponseContext("", "/newpage");

// данная строка возвратит текстовое сообщение клиенту и установит код возврата в 404
// в качестве ответа может быть использован готовый шаблон
return new ResponseContext("Page is not found", "", HttpStatusCode.NotFound);
```

## КЛАСС 'TemplateParser'

Класс шаблонизатора.
