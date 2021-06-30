# Argus, Hermes, Hygieia services
Code repository for Argus, Hermes, Hygieia services.

## Argus service
Argus service is responsible for scraping data. It is created with .NET 5 and gRPC service template from Visual Studio 2019. Argus consists of several services:
* Data scraper services
* Service status queue
* Data publish queue
* Unpublishable message queue
* Health check

![argus structure](https://github.com/weizhi-luo/stocks/blob/main/doc/images/argus.png)

For more details, please visit [Argus](https://github.com/weizhi-luo/stocks/tree/main/src/Argus).

## Hermes service
Hermes service receives data scraped by Argus from the work queue, deserializes and imports it to Metis database. It is created with .NET 5 with Web API template from Visual Studio 2019. Hermes consists of several services:
* Data importer service
* Data import status queue
* Unprocessable message queue
* Health check

![hermes_structure](https://github.com/weizhi-luo/stocks/blob/main/doc/images/hermes.png)

For more details, please visit [Hermes](https://github.com/weizhi-luo/stocks/tree/main/src/Hermes)
