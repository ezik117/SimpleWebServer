# MULTITHREADED WEB (HTTP) SERVER WITH TEMPLATE PARSER AND USER'S SESSION SUPPORT ON C#

[Russian version](https://github.com/ezik117/SimpleWebServer/blob/main/README.md)

## INTRODUCTION

Here is a simple HTTP-server with template paserer based on slightly modified Jinja language syntax with user's session support on the server side. It intends for usage in small C# projects where the HTTP based interfaces is needed. Configuring the server is very simple and requires to set only a handful of parameters in the following classes:

- [WebServerV1](#WebServerV1-CLASS)
- [RouteFunctions](#RouteFunctions-CLASS)

**WebServerV1** - this class is a heart of the system and contains all necessary methods for multithreaded processing and maintain the sessions. It uses a Route Table linked with **RouteFunctions** static methods to process HTTP requests.

**RouteFunctions** - the class is a "working horse", it contains all user-defined methods to process HTTP requests.

## WebServerV1 CLASS

The base class to run the web server. Listens to all incoming connections on 8080 port by default ("http://localhost:8080" prefix).

.... translation is in progress, please be patient! ....
