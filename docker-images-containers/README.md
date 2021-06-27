# Docker images and containers
## Docker images used in Stocks project
Stocks project consists of multiple services (Argus, Hermes and Hygieia), as well as a database server hosting relevant databases for the services, a message broker allowing data exchange between producer and consumer, and a logging system collecting service logs.

Argus, Hermes and Hygieia services are created using .NET 5. However, the database server, message broker and logging system are built using existing docker images:
* Microsoft SQL Server - mcr.microsoft.com/mssql/server:2019-latest
* RabbitMQ - rabbitmq:3-management
* Seq logging - datalust/seq:latest
### Microsoft SQL Server
Stocks project uses Microsoft SQL Server as its database server and it is built based on [Quickstart: Run SQL Server container images with Docker](https://docs.microsoft.com/en-us/sql/linux/quickstart-install-connect-docker?view=sql-server-ver15&pivots=cs1-bash).
### RabbitMQ
Stocks project uses RabbitMQ as its message broker and it is built by following the instructions [Downloading and Installing RabbitMQ](https://www.rabbitmq.com/download.html) and [Get Started with RabbitMQ on Docker](https://codeburst.io/get-started-with-rabbitmq-on-docker-4428d7f6e46b).
### Seq
Stocks project uses Seq as a centralized logging system for collecting logs from different services. The logging system is built following [Getting Started with Docker](https://docs.datalust.co/docs/getting-started-with-docker).
## Communication between docker containers
After docker images are successfully spinned up to run in containers, one way to allow them to communicate with each other by attaching them to the same network. Docker comes with a default networking driver: bridge network
![docker networks]()
