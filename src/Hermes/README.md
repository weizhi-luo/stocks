# Hermes service
Hermes service receives data scraped by Argus from the work queue, deserializes and imports it to Metis database. It is created with .NET 5 with Web API template from Visual Studio 2019. Hermes consists of several services:
* Data importer service
* Data import status queue
* Unprocessable message queue
* Health check

![hermes_structure](https://github.com/weizhi-luo/stocks/blob/main/doc/images/hermes.png)

## Data importer service
Data importer service receives data from the work queue. The data is a byte series and has to be deserialized into a ```DataToImport``` object. The ```DataToImport``` object containes information such as data to be imported, as well as the names of data scraper service and procedure. Data importer service uses these names to locate the related SQL server stored procedure from configuration for data import. The configuration is stored in appsettings.json file:

![hermes_data_import_appsettings](https://github.com/weizhi-luo/stocks/blob/main/doc/images/hermes_data_import_appsettings.PNG)

When data import service runs, it may finish successfully or experience different problems. For a successfully run, it generates a success status and sends this status to data import status queue service for record keeping. For an unsuccessful run, based on the cause, data import service generates an error status and sends it to either data import status queue or unprocessable message queue.

If the unsuccessful run is caused by failing to deserialize data from the work queue, an error will be sent to unprocessable message queue. If it is caused due to failure in deserializing data content to a ```DataTable``` object, locating SQL server stored procedure from configuration or saving data to Metis database, an error will be sent to data import status queue.

## Data import status queue and unprocessable message queue
As explained above, data import status queue and unprocessable message queue are services receving statuses and errors generated in data import service runs. 

The status received by data import queue contains not only status type, but also information such as source data scraper service and procedure names. The latest data import status, which is identified by source data scraper service and procedure names, is kept and it allows users or applications to check.

The error received by unprocessable message queue containes information including the message's consumer tag, delivery tag, redelivered status, exchange, routing key, basic properties, detail and time stamp. The error is stored so that users or applications can check.

## Health check
Health check includes multiple services which monitor the status of health of various processes and infrastructure:
* availability of SQL Server
* availability of work queue (RabbitMQ)
* data import status

### SQL server and RabbitMQ health checks
Since ASP .NET Core 2.2, [health checks](https://docs.microsoft.com/en-gb/dotnet/architecture/microservices/implement-resilient-applications/monitor-app-health) become a build-in feature of .NET Core. For checking the health status of SQL Server and RabbitMQ, the simplest way is to call ```Microsoft.Extensions.DependencyInjection.SqlServerHealthCheckBuilderExtensions.AddSqlServer()``` and ```Microsoft.Extensions.DependencyInjection.RabbitMQHealthCheckBuilderExtensions.AddRabbitMQ()``` methods in ```ConfigureServices()``` method in Startup.cs file:

![hermes_health_check_sql_mq](https://github.com/weizhi-luo/stocks/blob/main/doc/images/hermes_health_check_sql_mq.PNG)

### Data import health check
As illustrated above, data import status queue and unprocessable message queue services store status and error information. Customised services can be created to examine the status and error information so as to provide processes health check.

A customised health check service can be created by defining a class which implements interface ```Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck``` and method ```CheckHealthAsync()```. For example, in order to monitor the status of data import service, ```CheckHealthAsync()``` can check whether there are any Error statuses in data import status queue and/or messages in unprocessable message queue. If there are any, ```CheckHealthAsync()``` will return a result as unhealthy. The customised health check service can be injected in ```ConfigureServices()``` method in Startup.cs file:

![hermes_data_import_health_check](https://github.com/weizhi-luo/stocks/blob/main/doc/images/hermes_customised_data_import_health_check.PNG)

### Invoking health checks
To allow health checks to be invoked, a middleware is added to ```Configure()``` method in Startup.cs file to expose a "/healthcheck" endpoint. This endpoint can be remotely called by users, applications or services to invoke health checks.
![health_check_endpoint](https://github.com/weizhi-luo/stocks/blob/main/doc/images/health_check_endpoint.PNG)

## Web API
Status and error information stored in data import status queue and unprocessable message queue services not only facilitate health check services, but they also can be queried in Web APIs using contollers:

![hermes_controllers](https://github.com/weizhi-luo/stocks/blob/main/doc/images/hermes_controllers.PNG)

[NSwag](https://docs.microsoft.com/en-us/aspnet/core/tutorials/getting-started-with-nswag?view=aspnetcore-5.0&tabs=visual-studio) is used to present Swagger UI:

![hermes_swagger_ui](https://github.com/weizhi-luo/stocks/blob/main/doc/images/hermes_swagger_ui.PNG)

The Web APIs, for example DataImportStatus APIs presented above, return different statuses after executing data import. These results can be used for creating other applications such as a dashboard for showing real time process report.
