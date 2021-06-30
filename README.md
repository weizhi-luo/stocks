# Stocks project
Repository of Stocks project, which builds a system for stocks market data scrape, import and storage. 

![Overview](https://github.com/weizhi-luo/stocks/blob/main/doc/images/overview.png)

Stocks project consists of the following components:
* Data scrape service, Argus
* Data import service, Hermes
* A work queue built on RabbitMQ to allow data transmission from Argus to Hermes
* A database server with databases, Argus and Metis, built on Microsoft SQL Server
* Health check service, Hygieia, for monitoring health status of Argus, Hermes, work queue and databases
* A centralized logging system built on Seq to collect logs from Argus and Hermes
## Overview
Argus service is responsible for scraping data. It has gRPC services enabled and allows users to call specific methods remotely to trigger data scrape procedures. The scrape procedures may query Argus database for scraping records or other data to help defining logic. The data scraped by Argus is serialized and transmitted to a work queue and then received by Hermes service. Hermes service deserializes and processes the data and imports it to Metis database. 

Both Argus and Hermes services have health check services set up. These services can monitor the status of health of data scraping and import, as well as the availability of work queue and databases. Health checks can be invoked by calling endpoints exposed by the health check services and these calls return health status reports. These reports are used by Hygieia service to produce a web page that presents the health status:

![Health_check_ui](https://github.com/weizhi-luo/stocks/blob/main/doc/images/health_check_ui.PNG)

Argus, Hermes and Hygieia services are created using .NET 5. The source code can be found at [scr](https://github.com/weizhi-luo/stocks/tree/main/src). 

The work queue, database server and logging system are built using existing docker images. The details can be found at [docker-images-containers](https://github.com/weizhi-luo/stocks/tree/main/docker-images-containers). The scripts for creating and setting up the databases are at [database](https://github.com/weizhi-luo/stocks/tree/main/database).
