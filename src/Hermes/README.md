# Hermes service
Hermes service receives data scraped by Argus from the work queue, deserializes and imports it to Metis database. It is created with .NET 5 with Web API template from Visual Studio 2019. Hermes consists of several services:
* Data importer service
* Data import status queue
* Unprocessable message queue
* Health check

![hermes_structure](https://github.com/weizhi-luo/stocks/blob/main/doc/images/hermes.png)

## Data importer service
Data importer service receives data from the work queue. The data is a byte series and has to be deserialized into a DataToImport object. The DataToImport object containes information such as data to be imported, as well as the names of data scraper service and procedure. Data importer service uses these names of data scraper service and procedure to locate the related SQL server stored procedure from the configurate for data import. The configurate is stored in appsettings.json file:

![hermes_data_import_appsettings]("https://github.com/weizhi-luo/stocks/blob/main/doc/images/hermes_data_import_appsettings.PNG")


## Data import status queue and Unprocessable message queue


## Health check


## Web API
Status and error information stored in data import status queue and unprocessable message queue services not only facilitate health check services, but they also can be queried in Web APIs by using contollers:

![hermes_controllers](https://github.com/weizhi-luo/stocks/blob/main/doc/images/hermes_controllers.PNG)

[NSwag](https://docs.microsoft.com/en-us/aspnet/core/tutorials/getting-started-with-nswag?view=aspnetcore-5.0&tabs=visual-studio) is used to present Swagger UI:

![hermes_swagger_ui](https://github.com/weizhi-luo/stocks/blob/main/doc/images/hermes_swagger_ui.PNG)

The Web APIs, for example DataImportStatus APIs presented above, return different status when executing data import. These results can be used for creating more applications such as a dashboard for showing real time process report.
