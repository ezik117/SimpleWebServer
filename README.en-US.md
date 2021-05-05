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
----- | --- | --------
responseCodePage | string | The code page name of the returned string `ResponseContext.responseString` by the user's route function. The default value is "UTF-8". For Cyrillic from Visual Studio code (in the case when the `TemplateParser.ParseFromString` function is used) it should be" windows-1251 ".
staticContent | string | Specifies the location of static content if it is external, i.e. pictures, css files, etc. if they are stored in files, and are not an embedded resource (EmbeddedResource). Supports relative paths like (".. \\ .. \\"). The default value is the current directory.
useEmbeddedResources | bool | Shows where to get the static files. If set to True, then for all file requests (except for HTML pages that are processed in route functions, where the path to templates is directly specified) objects will be searched in Embedded Resources. If False, then files are searched in the staticContent directory. The default value is False.

### METHODS

Full declaration | Description
---------------- | --------
void WebServerV1(string prefix = "http://localhost:8080/") | Constructor. Launches a web server to listen and process requests. The Prefix is the prefix for the HttpListener class.
void Stop() | Shuts down the server gracefully. It is possible to terminate the program without calling this method.
public void AddRoute(string route, RouteFunction function) | Adds a routing function to the route table.

## RouteFunctions CLASS

Static class containing static methods executed if the requested address matches the specified route in the routing table of the ** WebServerV1 ** class. The class and its methods can be dynamic, but then it must be initialized before adding the methods to the routing table.

All methods of the class must meet to the definition of the delegate `public delegate ResponseContext RouteFunction (RequestContext context)` of the ** WebServerV1 ** class.

.... translation is in progress, please be patient! ....
