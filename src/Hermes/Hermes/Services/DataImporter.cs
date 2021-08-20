using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes
{
    public class DataImporter : BackgroundService
    {
        private readonly ILogger<DataImporter> _logger;
        private readonly string _serviceName;
        private readonly DataImportStatusQueue _dataImportStatusQueue;
        private readonly UnprocessableMessageQueue _unprocessableMessageQueue;
        private readonly IConnection _messageQueueConnection;
        private readonly IModel _messageQueueChannel;
        private readonly IBasicProperties _messageQueueChannelProperties;
        private readonly EventingBasicConsumer _consumer;
        private readonly ManualResetEventSlim _dataProcessingStoppedEvent;
        private readonly Dictionary<GrpcServiceProcedure, DataImportConfiguration> _dataImportConfigurations;
        private readonly string _connectionString;
        
        private bool _processingData;
        private bool _signaledToStopProcessData;

        public DataImporter(ILogger<DataImporter> logger, DataImportStatusQueue dataImportStatusQueue, UnprocessableMessageQueue unprocessableMessageQueue, IConfiguration configuration)
        {
            _logger = logger;
            _serviceName = nameof(DataImporter);
            _dataImportStatusQueue = dataImportStatusQueue;
            _unprocessableMessageQueue = unprocessableMessageQueue;

            var factory = new ConnectionFactory
            {
                HostName = configuration["MessageQueue:HostName"],
                UserName = configuration["MessageQueue:UserName"],
                Password = configuration["MessageQueue:Password"],
                AutomaticRecoveryEnabled = true
            };

            _messageQueueConnection = factory.CreateConnection();

            _messageQueueChannel = _messageQueueConnection.CreateModel();
            _messageQueueChannel.ConfirmSelect();
            _messageQueueChannel.QueueDeclare(
                queue: configuration["MessageQueue:Queue"],
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
            _messageQueueChannel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            _messageQueueChannelProperties = _messageQueueChannel.CreateBasicProperties();
            _messageQueueChannelProperties.Persistent = true;

            _consumer = new EventingBasicConsumer(_messageQueueChannel);
            _consumer.Received += (eventSender, eventArgs) => ProcessData(eventSender, eventArgs);
            _messageQueueChannel.BasicConsume(queue: configuration["MessageQueue:Queue"], autoAck: false, consumer: _consumer);

            _dataProcessingStoppedEvent = new ManualResetEventSlim(false);

            var dataImportConfigurations = new List<DataImportConfiguration>();
            configuration.GetSection("DataImportConfigurations").Bind(dataImportConfigurations);
            _dataImportConfigurations = dataImportConfigurations.ToDictionary(k => k.DataScrapeServiceProcedure, v => v);

            _connectionString = configuration.GetConnectionString("Metis");

            _processingData = false;
            _signaledToStopProcessData = false;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }

            _consumer.Received -= (eventSender, eventArgs) => ProcessData(eventSender, eventArgs);

            _signaledToStopProcessData = true;

            if (_processingData)
            {
                _dataProcessingStoppedEvent.Wait();
            }

            _messageQueueChannel?.Close();
            _messageQueueChannel?.Dispose();
            _messageQueueConnection?.Close();
            _messageQueueConnection?.Dispose();
        }

        private void ProcessData(object eventSender, BasicDeliverEventArgs eventArgs)
        {
            _processingData = true;

            _logger.LogInformation($"Service '{_serviceName}' starts to process messsage with delivery tag '{eventArgs.DeliveryTag}'");

            DataToImport dataToImport;
            try
            {
                dataToImport = ExtractDataToImport(eventArgs);
            }
            catch (Exception exception)
            {
                var errorInformation = "failed to deserialize data to import from message";

                _logger.LogError(exception, $"Service '{_serviceName}' {errorInformation}.");
                _messageQueueChannel.BasicReject(deliveryTag: eventArgs.DeliveryTag, requeue: false);
                
                EnqueueUnprocessableMessage(eventArgs, errorInformation, exception);

                CheckStopRequested();
                _processingData = false;

                return;
            }

            if (!_dataImportConfigurations.TryGetValue(dataToImport.DataScrapeServiceProcedure, out var dataImportConfiguration))
            {
                var errorInformation = 
                    $"failed to extract data import configuration for data scrape service '{dataToImport.DataScrapeServiceProcedure.Service}' procedure '{dataToImport.DataScrapeServiceProcedure.Procedure}'";

                _logger.LogError($"Service '{_serviceName}' {errorInformation}");
                _messageQueueChannel.BasicReject(deliveryTag: eventArgs.DeliveryTag, requeue: false);
                
                EnqueueDataImportStatus(dataToImport.DataScrapeServiceProcedure.Service, 
                    dataToImport.DataScrapeServiceProcedure.Procedure, Status.Error, errorInformation);

                CheckStopRequested();
                _processingData = false;

                return;
            }

            DataTable data;
            try
            {
                data = JsonConvert.DeserializeObject<DataTable>(dataToImport.Data);
            }
            catch (Exception exception)
            {
                var errorInformation = $"failed to deserialize data from:{Environment.NewLine}{dataToImport.Data}";

                _logger.LogError(exception, $"Service '{_serviceName}' {errorInformation}");
                _messageQueueChannel.BasicReject(deliveryTag: eventArgs.DeliveryTag, requeue: false);

                EnqueueDataImportStatus(dataToImport.DataScrapeServiceProcedure.Service, 
                    dataToImport.DataScrapeServiceProcedure.Procedure, Status.Error, errorInformation);

                CheckStopRequested();
                _processingData = false;

                return;
            }

            try
            {
                SaveData(dataImportConfiguration.DataImportStoredProcedure, dataImportConfiguration.DataImportStoredProcedureParameterName, data); 
            }
            catch (Exception exception)
            {
                var errorInformation = $"failed to save data to database";

                _logger.LogError(exception, $"Service '{_serviceName}' {errorInformation}.");
                _messageQueueChannel.BasicReject(deliveryTag: eventArgs.DeliveryTag, requeue: false);

                EnqueueDataImportStatus(dataToImport.DataScrapeServiceProcedure.Service,
                    dataToImport.DataScrapeServiceProcedure.Procedure, Status.Error, errorInformation);

                CheckStopRequested();
                _processingData = false;

                return;
            }

            var successInformation =
                    $"finished saving data with delivery tag '{eventArgs.DeliveryTag} for data scrape service '{dataToImport.DataScrapeServiceProcedure.Service}' procedure '{dataToImport.DataScrapeServiceProcedure.Procedure}'";

            _logger.LogInformation($"Service '{_serviceName}' {successInformation}");
            _messageQueueChannel.BasicAck(deliveryTag: eventArgs.DeliveryTag, multiple: false);

            EnqueueDataImportStatus(dataToImport.DataScrapeServiceProcedure.Service,
                dataToImport.DataScrapeServiceProcedure.Procedure, Status.Success, successInformation);

            CheckStopRequested();
            _processingData = false;
        }

        private void SaveData(string storedProcedure, string parameterName, DataTable data)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = storedProcedure;
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue(parameterName, data);

                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
        }

        private void EnqueueDataImportStatus(string service, string procedure, Status status, string detail)
        {
            _dataImportStatusQueue.EnqueueDataImportStatus(
                new DataImportStatus
                {
                    DataScrapeServiceProcedure = new GrpcServiceProcedure
                    {
                        Service = service,
                        Procedure = procedure
                    },
                    Status = status,
                    Detail = detail,
                    UtcTimestamp = DateTime.UtcNow
                });
        }

        private void EnqueueUnprocessableMessage(BasicDeliverEventArgs eventArgs, string errorInformation, Exception exception)
        {
            _unprocessableMessageQueue.EnqueueUnprocessableMessage(new UnprocessableMessage
            {
                ConsumerTag = eventArgs.ConsumerTag,
                DeliveryTag = eventArgs.DeliveryTag,
                Redelivered = eventArgs.Redelivered,
                Exchange = eventArgs.Exchange,
                RoutingKey = eventArgs.RoutingKey,
                BasicProperties = eventArgs.BasicProperties,
                Detail = $"{errorInformation}{Environment.NewLine}{exception}",
                UtcTimestamp = DateTime.UtcNow
            });
        }

        private DataToImport ExtractDataToImport(BasicDeliverEventArgs eventArgs)
        {
            var body = eventArgs.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            
            var dataToImport = JsonConvert.DeserializeObject<DataToImport>(message);
            if (dataToImport.DataScrapeServiceProcedure == null)
            {
                throw new InvalidCastException("Failed to deserialze DataScrapeServiceProcedure from data to import");
            }

            return dataToImport;
        }

        private void CheckStopRequested()
        {
            if (!_signaledToStopProcessData)
            {
                return;
            }

            if (!_dataProcessingStoppedEvent.IsSet)
            {
                _dataProcessingStoppedEvent.Set();
            }
        }
    }
}
