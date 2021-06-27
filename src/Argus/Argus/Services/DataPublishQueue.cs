using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Argus
{
    public class DataPublishQueue
    {
        private readonly ConcurrentQueue<DataToPublish> _dataToPublish;
        private readonly ILogger<DataPublishQueue> _logger;
        private readonly ConcurrentDictionary<GrpcServiceProcedure, DataPublishError> _latestDataPublishErrors;
        private readonly ConcurrentDictionary<ulong, DataToPublish> _outstandingMessageConfirms;
        private readonly UnpublishableMessageQueue _unpublishableMessageQueue;
        private readonly CancellationToken _cancellationToken;
        private readonly string _messageQueueQueueName;
        private readonly string _serviceName;
        private readonly IConfiguration _configuration;
        private readonly IConnection _messageQueueConnection;
        private readonly IModel _messageQueueChannel;
        private readonly IBasicProperties _messageQueueChannelProperties;

        private uint _isMonitorLoopStarted;
        
        public DataPublishQueue(ILogger<DataPublishQueue> logger, UnpublishableMessageQueue unpublishableMessageQueue, IHostApplicationLifetime applicationLifeTime, IConfiguration configuration)
        {
            _dataToPublish = new ConcurrentQueue<DataToPublish>();
            _logger = logger;
            _latestDataPublishErrors = new ConcurrentDictionary<GrpcServiceProcedure, DataPublishError>();
            _outstandingMessageConfirms = new ConcurrentDictionary<ulong, DataToPublish>();
            _unpublishableMessageQueue = unpublishableMessageQueue;
            _cancellationToken = applicationLifeTime.ApplicationStopping;
            _messageQueueQueueName = configuration["MessageQueue:Queue"];
            _serviceName = nameof(DataPublishQueue);
            _configuration = configuration;
            
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
            _messageQueueChannel.BasicReturn += (_, eventArg) => ProcessBasicReturn(eventArg);
            _messageQueueChannel.BasicAcks += (_, eventArg) => RemoveOutstandingMessageConfirm(eventArg.DeliveryTag);
            _messageQueueChannel.BasicNacks += (_, eventArg) => ProcessBasicNack(eventArg.DeliveryTag);
            
            _messageQueueChannelProperties = _messageQueueChannel.CreateBasicProperties();
            _messageQueueChannelProperties.Persistent = true;

            _isMonitorLoopStarted = 0;
        }

        public void StartMonitor()
        {
            var isMonitoring = Interlocked.CompareExchange(ref _isMonitorLoopStarted, 1, 0);
            if (isMonitoring == 1)
            {
                _logger.LogInformation($"Service '{_serviceName}' is already monitoring data for publish.");
                return;
            }

            if (_cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation($"Service '{_serviceName}' is cancelled before starting to monitor data for publish.");
                return;
            }

            var monitorTask = Task.Factory.StartNew(Monitor, _cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            if (monitorTask.IsCanceled)
            {
                _logger.LogInformation($"Service '{_serviceName}' is cancelled to before starting to monitor data for publish.");
            }
            else
            {
                _logger.LogInformation($"Service '{_serviceName}' starts to monitor data for publish.");
            }
        }

        private void Monitor()
        {
            var wait = new ManualResetEventSlim(false);

            while (!_cancellationToken.IsCancellationRequested)
            {
                if (_dataToPublish.IsEmpty)
                {
                    try
                    {
                        wait.Wait(1000, _cancellationToken);
                        continue;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }

                if (!_dataToPublish.TryDequeue(out var dataToPublish))
                {
                    continue;
                }

                try
                {
                    var sequenceNumber = _messageQueueChannel.NextPublishSeqNo;
                    _logger.LogInformation($"Service '{_serviceName}' starts to publish data generated by service '{dataToPublish.ServiceProcedure.Service}' procedure '{dataToPublish.ServiceProcedure.Procedure}' with sequence number '{sequenceNumber}'");

                    var dataToPublishBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(dataToPublish));

                    _messageQueueChannel.BasicPublish(
                        exchange: "", 
                        routingKey: _messageQueueQueueName,
                        mandatory: true,
                        basicProperties: _messageQueueChannelProperties, 
                        body: dataToPublishBytes
                    );

                    _outstandingMessageConfirms.TryAdd(sequenceNumber, dataToPublish);
                    _latestDataPublishErrors.TryRemove(dataToPublish.ServiceProcedure, out _);
                    _logger.LogInformation($"Service '{_serviceName}' finished publishing data generated by service '{dataToPublish.ServiceProcedure.Service}' procedure '{dataToPublish.ServiceProcedure.Procedure}' with sequence number '{sequenceNumber}'");
                }
                catch (Exception exception)
                {
                    var errorMessage = $"failed to publish data generated by service '{dataToPublish.ServiceProcedure.Service}' procedure '{dataToPublish.ServiceProcedure.Procedure}'";
                    
                    _latestDataPublishErrors.AddOrUpdate(dataToPublish.ServiceProcedure, 
                        new DataPublishError
                        {
                            ServiceProcedure = dataToPublish.ServiceProcedure,
                            Error = $"{errorMessage}{Environment.NewLine}{exception}"
                        }, 
                        (k ,v) =>
                        {
                            v.Error = $"{errorMessage}{Environment.NewLine}{exception}";
                            return v;
                        });
                    _logger.LogError(exception, $"Service '{_serviceName}' {errorMessage}");
                }
            }

            _isMonitorLoopStarted = 0;

            _messageQueueChannel?.Close();
            _messageQueueChannel?.Dispose();
            _messageQueueConnection?.Close();
            _messageQueueConnection?.Dispose();

            _logger.LogInformation($"Service '{_serviceName}' is signaled to stop.");
        }

        public void EnqueueDataToPublish(DataToPublish dataToPublish)
        {
            _dataToPublish.Enqueue(dataToPublish);
        }

        public ICollection<DataPublishError> GetLatestDataPublishErrors()
        {
            return _latestDataPublishErrors.Values;
        }

        private void RemoveOutstandingMessageConfirm(ulong sequenceNumber)
        {
            _outstandingMessageConfirms.TryRemove(sequenceNumber, out _);
        }

        private void ProcessBasicReturn(RabbitMQ.Client.Events.BasicReturnEventArgs eventArg)
        {
            var errorMessageBuilder = new StringBuilder();
            errorMessageBuilder.AppendLine("failed to publish data due to return from broker.");
            errorMessageBuilder.AppendLine($"Exchange:{eventArg.Exchange}");
            errorMessageBuilder.AppendLine($"ReplyCode:{eventArg.ReplyCode}");
            errorMessageBuilder.AppendLine($"ReplyText:{eventArg.ReplyText}");
            errorMessageBuilder.AppendLine($"RoutingKey:{eventArg.RoutingKey}");
            errorMessageBuilder.AppendLine($"BasicProperties:{eventArg.BasicProperties}");

            _logger.LogError($"Service '{_serviceName}' {errorMessageBuilder}");
            _unpublishableMessageQueue.EnqueueUnpublishableMessage(new UnpublishableMessage
            {
                BasicProperties = eventArg.BasicProperties,
                Exchange = eventArg.Exchange,
                ReplyCode = eventArg.ReplyCode,
                ReplyText = eventArg.ReplyText,
                RoutingKey = eventArg.RoutingKey,
                UtcTimestamp = DateTime.UtcNow
            });
        }

        private void ProcessBasicNack(ulong sequenceNumber)
        {
            if (!_outstandingMessageConfirms.TryRemove(sequenceNumber, out var dataToPublish))
            {
                return;
            }

            var errorMessage = $"failed to publish data generated by service '{dataToPublish.ServiceProcedure.Service}' procedure '{dataToPublish.ServiceProcedure.Procedure}' with sequence number '{sequenceNumber}' as it is nack-ed by the message queue.";
            
            _latestDataPublishErrors.AddOrUpdate(dataToPublish.ServiceProcedure,
                new DataPublishError
                {
                    ServiceProcedure = dataToPublish.ServiceProcedure,
                    Error = errorMessage
                }, 
                (k, v) =>
                {
                    v.Error = errorMessage;
                    return v;
                });
            _logger.LogError($"Service '{_serviceName}' {errorMessage}");
        }
    }

    public class DataToPublish
    {
        public GrpcServiceProcedure ServiceProcedure { get; set; }
        public string Data { get; set; }
    }

    public class DataPublishError
    {
        public GrpcServiceProcedure ServiceProcedure { get; set; }
        public string Error { get; set; }
    }
}
