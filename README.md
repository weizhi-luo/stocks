# Stocks
Repository of Stocks project, which builds the system for stocks market data scrape, import and storage. The overview of system structure is shown below:

![Overview](https://github.com/weizhi-luo/stocks/blob/main/doc/images/overview.png)

Stocks project consists of the following components:
* Data scraping service, __Argus__
* Data import service, __Hermes__
* A work queue built on RabbitMQ to allow data transmission from Argus to Hermes
* A database server built on Microsoft SQL Server
* Health check service, __Hygieia__, for monitoring health status of Argus, Hermes, work queue and databases
* A centralized logging system built on Seq to collect logs from Argus and Hermes

Argus, Hermes and Hygieia services are created using .NET 5. The details can be found at [scr](https://github.com/weizhi-luo/stocks/tree/main/src).

The work queue, database server and logging system are built using existing docker images. The details can be found at [docker-images-containers](https://github.com/weizhi-luo/stocks/tree/main/docker-images-containers).
