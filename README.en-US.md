# MULTITHREADED WEB (HTTP) SERVER WITH TEMPLATE PARSER AND USER'S SESSION SUPPORT ON C#

[Russian version](README.md)

## INTRODUCTION

Here is a simple HTTP-server with template paserer based on slightly modified Jinja language syntax with user's session support on the server side. It intends for usage in small C# projects where the HTTP based interfaces is needed. Configuring the server is very simple and requires to set only a handful of parameters in the following classes:

- [WebServerV1](#WebServerV1-CLASS)
- [RouteFunctions](#RouteFunctions-CLASS)

**WebServerV1** - this class is a heart of the system and contains all necessary methods for multithreaded processing and maintain the sessions. It uses a Route Table linked with **RouteFunctions** static methods to process HTTP requests.

**RouteFunctions** - the class is a "working horse", it contains all user-defined methods to process HTTP requests.

## WebServerV1 CLASS

The base class to run the web server. Listens to all incoming connections on 8080 port by default ("http://localhost:8080" prefix).

In case the web server is used to accept requests on any of network interfaces, the following configuration is required:

- initialize the server with prefix `prefix="http://+:8080/"`
- add permission in the Windows command line `netsh http add urlacl url=http://+:8080/`

Remark: to see current network permission use: `netsh http show urlacl`. To remove permission use: `netsh http delete urlacl url=http://+:8080/`

> ATTENTION: This web server does not control hanged processing threads as well as does not control the number of running threads.

### PROPERTIES

Name | Type | Description
---- | ---- | -----------
responseCodePage | string | The code page name of the returned string `ResponseContext.responseString` by the user's route function. The default value is "UTF-8". For Cyrillic from Visual Studio code (in the case when the `TemplateParser.ParseFromString` function is used) it should be "windows-1251".
staticContent | string | Specifies the location of static content if it is external, i.e. pictures, css files, etc. if they are stored in files, and are not an embedded resource (EmbeddedResource). Supports relative paths like (".. \\ .. \\"). The default value is the current directory.
useEmbeddedResources | bool | Shows where to get the static files. If set to True, then for all file requests (except for HTML pages that are processed in route functions, where the path to templates is directly specified) objects will be searched in Embedded Resources. If False, then files are searched in the staticContent directory. The default value is False.

### METHODS

Full declaration | Description
---------------- | --------
void WebServerV1(string prefix = "http://localhost:8080/") | Constructor. Launches a web server to listen and process requests. The Prefix is the prefix for the HttpListener class.
void Stop() | Shuts down the server gracefully. It is possible to terminate the program without calling this method.
public void AddRoute(string route, RouteFunction function) | Adds a route function to the route table.

## RouteFunctions CLASS

Static class containing static methods executed if the requested address matches the specified route in the routing table of the **WebServerV1** class. The class and its methods can be dynamic, but then it must be initialized before adding the methods to the routing table.

All methods of the class must meet to the definition of the delegate `public delegate ResponseContext RouteFunction (RequestContext context)` of the **WebServerV1** class.

### REQUEST CONTEXT

The variable **context** of the **SessionData** class containing the context of the request is passed to the route function for handling the request from the client. The properties and methods of this variable are given below:

Property Name | Type | Description
------------- | ---- | --------
Method | RequestMethod | Enumeration. Contains the HTTP request method.
Route | string | The requested URL starts with "/". For example: "/" = "http://localhost:8080", "/login" = "http://localhost:8080/login" etc.
parameters | Dictionary<string, string> | Dictionary pairs of the HTTP request.
templateVariables | Dictionary<string, object> | A pre-built dictionary for the HTML template. Contains a **SessionData** object named 'session' by default. You can use your dictionaries to pass variables to the **TemplateParser** template engine.
session | SessionData | Direct reference to the user session object of the **SessionData** type. Direct use is not recommended. To manage sessions, you must use the methods of the **SessionManager** class.
sessionManager | SessionManager | A reference to the **SessionManager** class to manage the user session.
baseRequest | HttpListenerRequest | Reference to the original **HttpListenerRequest** class to get for more request parameters.

Full declaration code | Description
--------------------- | -----------
public string GetParam(string name, string defaultValue = "") | Returns the value of the parameter received via GET / POST. This method is preferable than direct access to the values dictionary.
public int GetParamsCount() | Returns the number of HTTP parameters in the request.

### RESPONSE CONTEXT

The route function must return an object of class **ResponseContext** declared as `public ResponseContext (string responseString =" ", string redirectUrl =" ", HttpStatusCode exitCode = HttpStatusCode.OK)`, where:

- **responseString** - HTML string to respond to the client. It can be either in the form of plain text (for example, for responses in XML or JSON format), or in the form of a preprocessed template via the **TemplateParser** template engine.
- **redirectUrl** - redirect string. By default it equals to an empty string and can be omitted in response. If it is not equal to an empty string, then it triggers a server response telling the client that the resource has been moved and it is necessary to go to another page. This kind of the redirect uses the client to redirect to another page or web resource. If you need to use the execution of another route function from processing one, then it must be called directly from the code. At the same time, do not forget that if a Request object from the original function is passed to another route function, then it will contain all the parameters of the current request. If the parameters are changed, they must be manually altered through the **parameters** property of the **SessionData** object.
- **exitCode** - server response code to the client. The default is 200 - OK. May be omitted.

## EXAMPLES

### Creating a server and a test page

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

### Retrieving data from a web page

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

### Using a user session

```C#
// Route: "/logon"
public static ResponseContext Logon(RequestContext context)
{
    if (context.GetParam("login") == "test" && context.GetParam("password") == "1")
    {
        // session is created automatically as soon as there is at least one key.
        // the session exists until the web browser are running by default.
        context.sessionManager.SessionSetKey(ref context.session, "user", "test");

        // you can manually set the session time. here - 24 hours.
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
    // check access
    if (context.sessionManager.SessionGetKey(context.session, "user") == null)
    {
        // user is not logged in - redirect to the main page
        return new ResponseContext("", "/");
    }

    // any type of objects can be stored in the session
    List<int> list = context.sessionManager.SessionGetKey(context.session, "list", new List<int>());
    list.Add(10);
    context.sessionManager.SessionSetKey(ref context.session, "list", list);
}
```

### Page redirection and http error templates

```C#
// this line will return an empty response to the client's browser with a redirect
// in the Header to the "newpage" page
return new ResponseContext("", "/newpage");

// this line will return a text message to the client and set the return code to 404.
// an existed template can be used as an answer.
return new ResponseContext("Page is not found", "", HttpStatusCode.NotFound);
```

## TemplateParser CLASS

Template generator class. Parses an input pattern passed either as a string or file.
During the development of the class, the Jinja language was taken as a basis.

### METHODS

Full declaration code | Description
--------------------- | -----------
string ParseFromString(string template, Dictionary<string, object> data = null, Encoding fileEncoding = null) | Returns the parsed text of the template from a string.
string ParseFromFile(string filename, Dictionary<string, object> data = null, Encoding encoding = null) | Returns the parsed text of the template from a file.
string ParseFromResource(string resource, Dictionary<string, object> data = null) | Returns the parsed text of the template from an embedded resource of assembly.

### LANGUAGE OF TEMPLATES

The template language consists of *variables* and *commands*. The first are specified through double curly braces `{{<variable name>}}`, the second through a combination of double brackets and percent sign `{% <command>%}`. The syntax for the template language control code is as follows:

<opening escape sequence `{{` or `{%`> <optional left space control specifier> <space (s)> <command or variable> <space (s)> <optional right space control specifier> <closing escape sequence ` }} `or`%} `>



.... translation is in progress, please be patient! ....
