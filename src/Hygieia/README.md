# Hygieia service
Hygieia is a [watch dog](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/monitor-app-health#use-watchdogs) service that watches and reports health status. It is created with .NET 5 and Web application template from Visual Studio 2019. 

It imports [AspNetCore.HealthChecks.UI](https://www.nuget.org/packages/AspNetCore.HealthChecks.UI/5.0.1), [AspNetCore.HealthChecks.UI.Client](https://www.nuget.org/packages/AspNetCore.HealthChecks.UI.Client/5.0.1) and [AspNetCore.HealthChecks.UI.InMemory.Storage](https://www.nuget.org/packages/AspNetCore.HealthChecks.UI.InMemory.Storage/5.0.1/) NuGet packages. The health check UI is injected to ```ConfigureServices()``` method in Startup.cs file:

![hygieia_inhect_health_check_ui](https://github.com/weizhi-luo/stocks/blob/main/doc/images/hygieia_inject_health_check_ui.PNG)

The "/healthcheck" endpoints exposed in [Argus](https://github.com/weizhi-luo/stocks/tree/main/src/Argus) and [Hermes](https://github.com/weizhi-luo/stocks/tree/main/src/Hermes) services can be called by Hygieia service by add the following settings in appsettings.json file:
```
   "HealthChecksUI": {
    "HealthChecks": [
      {
        "Name": "Argus",
        "Uri": "http://Argus:5000/healthcheck"
      },
      {
        "Name": "Hermes",
        "Uri": "http://Hermes:80/healthcheck"
      }
    ],
    "EvaluationTimeInSeconds": 20,
    "MinimumSecondsBetweenFailureNotifications": 60
  },
```

```EvaluationTimeInSeconds``` specifies the number of seconds between each call of Argus and Hermes services "/healthcheck" endpoints by Hygieia.

Hygieia service exposes a "/HealthChecks-UI#/HealthChecks" endpoint by adding a middleware to ```Configure()``` method in Startup.cs file:
> endpoints.MapHealthChecksUI();

By calling "/HealthChecks-UI#/HealthChecks" endpoint, health status of Argus and Hermes services can be presented in a web page:

![Health_check_ui](https://github.com/weizhi-luo/stocks/blob/main/doc/images/health_check_ui.PNG)
