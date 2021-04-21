# МНОГОПОТОЧНЫЙ ВЕБ-СЕРВЕР С ШАБЛОНИЗАТОРОМ И ПОДДЕРЖКОЙ СЕССИЙ

## ВВЕДЕНИЕ

Простой многопоточный сервер с поддержкой пользовательских сессий и встроенным шаблонизатором по принципу Jinja для использования в проектах C#, где необходим веб-интерфейс.

Работа сервера строится на настройке двух классов:

- WebServerV1
- RouteFunctions

**WebServerV1** - класс содержащий все необходимые действия по многопоточной обработке запросов на базе таблицы маршрутизации и обслуживанию сессий.

**RouteFunctions** - статический класс содержащий методы которые будет вызывать таблица маршрутизации.

## КЛАСС WEBSERVERV1

Основной класс для запуска веб сервера. По умолчанию обрабатывает входящие запросы на 8080 порту. Без заданной таблицы маршрутизации выдает "hello world!" в качестве начальной страницы.

### СВОЙСТВА

Имя свойства | Тип | Описание
------------ | --- | --------
responseCodePage | string | Имя кодовой страницы текста шаблона для ответа сервера клиенту. По умолчанию "UTF-8". Для кириллицы из кода Visual Studio должна быть "windows-1251".
sessionDuration | double | Срок хранения сессий на сервере в минутах. По умолчанию 1 день. (Примечание: данные сессий хранятся в памяти.)
staticContent | string | Задает место расположения статического контента, в случае если он является внешним, т.е. картинки, файлы css и т.п хранятся в файлах, а не являются встроенным ресурсом (EmbeddedResource). Поддерживает относительные пути вида ("..\\..\\"). Значение по умолчанию - текущая директория.
useEmbeddedResources | bool | Показывает откуда брать статические файлы. Если установлен в True, то на все запросы файлов (кроме HTML страниц, которые обрарабатываются в route-функциях, где напрямую указывается путь к шаблонам) объекты будут искаться в Embedded Resources. Если False, то файлы ищутся в директории staticContent. По умолчанию равен False.
values | Dictionary<string, object> | Словарь значений, передаваемых в шаблонизатор.

### МЕТОДЫ

Полный код объявления | Описание
--------------------- | --------
void WebServerV1(string prefix = "http://localhost:8080/") | Конструктор. Запускает веб сервер на прослушивание и обработку запросов. Префикс является префиксом класса HttpListener.
void Stop() | Корректно завершает работу сервера. Можно завершать программу без вызова данного метода.
public void AddRoute(string route, RouteFunction function) | Добавляет маршрут перехода в таблицу маршрутов.

## ПРИМЕРЫ

Создание сервера и базовой страницы:

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
        Dictionary<string, object> data = new Dictionary<string, object>();
        data.Add("dateNow", DateTime.Now);

        TemplateParser tp = new TemplateParser(data);
        
        return new ResponseContext(tp.ParseFromString(@"<HTML><BODY>Today is {{dateNow}}</BODY></HTML>", data));
    }
}

```
