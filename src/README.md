# Argus, Hermes, Hygieia services
Code repository for Argus. Hermes, Hygieia services.

## Argus service
Argus service is responsible for scraping data. It is created with .NET 5 and gRPC service template from Visual Studio 2019. Argus consists of several services:
* Data scraper services
* Service status queue
* Data publish queue
* Unpublishable message queue
* Health check

![argus structure](https://github.com/weizhi-luo/stocks/blob/main/doc/images/argus.png)

For more details, please visit [Argus](https://github.com/weizhi-luo/stocks/tree/main/src/Argus).
