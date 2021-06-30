# Argus service
Argus service is responsible for scraping data. It is created with .NET 5 and gRPC service template from Visual Studio 2019. Argus consists of several services:
* Data scraper services
* Service status queue
* Data publish queue
* Unpublishable message queue
* Health check

![argus structure](https://github.com/weizhi-luo/stocks/blob/main/doc/images/argus.png)

## Data scraper services and service status queue
Data scraper services contain logics for scraping data from various sources. They are gRPC services so that users can call specific methods remotely to trigger data scrape procedures. Data scrape procedures may query Argus database for scraping records or other data to define proper logic. When a data scrape procedure runs, it may finish successfully or experience different problems. Data scraper services categorise procedure run results into different status types: Success, Warning, Error and Information. The status is sent by a data scraper service to service status queue. Service status queue is a service that receives and stores latest data scrape procedure run status.

After a data scraper procedure successfully finishes scraping, the data is sent to data publish queue.

## Data publish queue and unpublishable message queue
Data publish queue is a service responsible for receiving scraped data, serializing it and publishing a message to a work queue built with RabbitMQ. The publish may fail. In order to detect failures in data publish, [publisher-side confirmation mechanism](https://www.rabbitmq.com/confirms.html#publisher-confirms) is used. 

If the message is succcessfully handled by the work queue's message broker, a basic.ack command will be returned. 

If the message broker is unable to handle the message, a basic.nack command will be returned and an error will be created and kept. The error contains information such as data scraper service and related procedure names so as to allow users to understand which service/procedure is affected by the failure. 

If the message cannot be routed to the queue specified, a basic.return command will be returned. An error will be created with related work queue exchange, reply code, reply text, routing key, basic properties and time stamp. The error will be sent to the unpublishable message queue service. Unpublishable message queue stores the error and allows users to check what message is unpublishable. 

## Health check
Health check includes multiple services which monitor the status of health of various processes and infrastructure:
* availability of SQL Server
* availability of work queue (RabbitMQ)
* data scraper services status
* data publish status

### SQL server and RabbitMQ health checks
Since ASP .NET Core 2.2, [health checks](https://docs.microsoft.com/en-gb/dotnet/architecture/microservices/implement-resilient-applications/monitor-app-health) become a build-in feature of .NET Core. For checking the health status of SQL Server and RabbitMQ, the simplest way is to call ```Microsoft.Extensions.DependencyInjection.SqlServerHealthCheckBuilderExtensions.AddSqlServer()``` and ```Microsoft.Extensions.DependencyInjection.RabbitMQHealthCheckBuilderExtensions.AddRabbitMQ()``` methods in ```ConfigureServices()``` method in Startup.cs file:

![health_check_sql_mq](https://github.com/weizhi-luo/stocks/blob/main/doc/images/health_check_sql_mq.PNG)

### Data scraper services and data publsih health check
As illustrated above, service status queue, data publish queue and unpublishable message queue services store status and error information. Based on this status and error information, customised services can be created to carry out processes health check. 

A customised health check service can be created by defining a class which implements interface ```Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck``` and method ```CheckHealthAsync()```. For example, in order to monitor the status of data scraper services, ```CheckHealthAsync()``` can whether there are any Error and Warning statuses in service status queue. If there are any, ```CheckHealthAsync()``` can return a result as unhealthy with error/warning details. The customised health check services can be injected in ```ConfigureServices()``` method in Startup.cs file:

![argus_customised_health_checks](https://github.com/weizhi-luo/stocks/blob/main/doc/images/argus_customised_health_checks.PNG)

### Invoking health checks
To invoke health checks, a middleware is added to ```Configure()``` method in Startup.cs file to expose a "/healthcheck" endpoint. This endpoint can be remotely called by users, applications or services to have health checks invoked. 

![health_check_endpoint](https://github.com/weizhi-luo/stocks/blob/main/doc/images/health_check_endpoint.PNG)

## Web API
Status and error information stored in service status queue, data publish queue and unpublishable message queue services not only facilitate health check services, but they also can be queried in Web APIs by using contollers:

![contoller](https://github.com/weizhi-luo/stocks/blob/main/doc/images/controllers.PNG)

[NSwag](https://docs.microsoft.com/en-us/aspnet/core/tutorials/getting-started-with-nswag?view=aspnetcore-5.0&tabs=visual-studio) is used to present Swagger UI:

![swagger_ui](https://github.com/weizhi-luo/stocks/blob/main/doc/images/swagger_ui.PNG)

The Web APIs, for example GrpcServiceProcedureStatus APIs presented above, return different status when executing data scraper services and procedures. These results can be used for creating more applications such as a dashboard for showing real time process report. 
