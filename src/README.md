## Argus service
Argus service is responsible for scraping data. It is created with .NET 5 and gRPC service template from Visual Studio 2019. Argus consists of several services:
* Data scraper services
* Service status queue
* Data publish queue
* Unpublishable message queue
* Health check

![argus structure](https://github.com/weizhi-luo/stocks/blob/main/doc/images/argus.png)

### Data scraper services and service status queue
Data scraper services contain logics for scraping data from various sources. They are gRPC services so that users can call specific methods remotely to trigger data scrape procedures. Data scrape procedures may query Argus database for scraping records or other data to define proper logic. When a data scrape procedure runs, it may finish successfully or experience different problems. Data scraper services categorise procedure run results into different status types: Success, Warning, Error and Information. The status is sent by a data scraper service to service status queue. Service status queue is a service that receives and stores latest data scrape procedure run status.

After a data scraper procedure successfully finishes scraping, the data is sent to data publish queue.

### Data publish queue and unpublishable message queue
Data publish queue is a service responsible for receiving scraped data, serializing it and publishing a message to a work queue built with RabbitMQ. The publish may fail. In order to detect failures in data publish, [publisher-side confirmation mechanism](https://www.rabbitmq.com/confirms.html#publisher-confirms) is used. 

If the message is succcessfully handled by the work queue's message broker, a basic.ack command will be returned. 

If the message broker is unable to handle the message, a basic.nack command will be returned and an error will be created and kept. The error contains information such as data scraper service and related procedure names so as to allow users to understand which service/procedure is affected by the failure. 

If the message cannot be routed to the queue specified, a basic.return command will be returned. An error will be created with related work queue exchange, reply code, reply text, routing key, basic properties and time stamp. The error will be sent to the unpublishable message queue service. Unpublishable message queue stores the error and allows users to check what message is unpublishable. 

### Health check
