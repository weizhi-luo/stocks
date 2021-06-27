# Docker images and containers
## Docker images used in Stocks project
Stocks project consists of multiple services (Argus, Hermes and Hygieia), as well as a database server hosting relevant databases for the services, a message broker allowing data exchange, and a logging system collecting service logs.

Argus, Hermes and Hygieia services are created using .NET 5. However, the database server, message broker and logging system are built using existing docker images for the solutions below:
* Microsoft SQL Server: 
* RabbitMQ
* Seq logging
### Microsoft SQL Server
Stocks project uses Microsoft SQL Server as its database server. It pulls docker image __*mcr.microsoft.com/mssql/server:2019-latest*__ and builds a database server based on the documentation at [Quickstart: Run SQL Server container images with Docker](https://docs.microsoft.com/en-us/sql/linux/quickstart-install-connect-docker?view=sql-server-ver15&pivots=cs1-bash).
### RabbitMQ
Stocks project uses RabbitMQ as its message broker. The docker image used is __*rabbitmq:3-management*__ and it is built by following the instructions at [Downloading and Installing RabbitMQ](https://www.rabbitmq.com/download.html) and [Get Started with RabbitMQ on Docker](https://codeburst.io/get-started-with-rabbitmq-on-docker-4428d7f6e46b).
### Seq
Stocks project uses Seq as a centralized logging system for collecting logs from different services. Docker image __*datalust/seq:latest*__ is used and the logging system is built following [Getting Started with Docker](https://docs.datalust.co/docs/getting-started-with-docker).
## Communication between docker containers
After docker images are successfully configured to run in containers, one way to allow them to communicate with each other is by attaching them to the same network. Docker comes with a default networking driver, bridge network, and all containers are attached to it.

![docker networks](https://github.com/weizhi-luo/stocks/blob/main/docker-images-containers/docker%20network%20list.PNG)

In the bridge netwrork, each container is assigned with an IP address and the containers can communicate with each other using the IP addresses. A container's IP address can be found by using command:
> docker inspect <container_id> | grep IPAddress

However, the IP address of a container can change and an unstable IP address will make container communication cumbersome or even impossible. One way to avoid this problem is to create an user-defined network and connect existing containers to it. After that, containers on the same user defined network can communicate with each other by names. 

An user-defined network can be created by:
> docker network create <network_name>

Then, we can connect an existing container to it:
> docker network connect <network_name> <container_name>

More details can be found at [How To Communicate Between Docker Containers](https://www.tutorialworks.com/container-networking/).
