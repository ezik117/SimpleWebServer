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

.... translation is in progress, please be patient! ....
