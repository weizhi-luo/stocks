using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Argus
{
    public class UnpublishableMessageQueue
    {
        private readonly ConcurrentQueue<UnpublishableMessage> _unpublishableMessages;
        private readonly ConcurrentDictionary<string, UnpublishableMessage> _latestUnpublishableMessages;
        private readonly ILogger<UnpublishableMessageQueue> _logger;
        private readonly CancellationToken _cancellationToken;
        private readonly string _serviceName;

        private uint _isMonitorLoopStarted;

        public UnpublishableMessageQueue(ILogger<UnpublishableMessageQueue> logger, IHostApplicationLifetime applicationLifetime)
        {
            _unpublishableMessages = new ConcurrentQueue<UnpublishableMessage>();
            _latestUnpublishableMessages = new ConcurrentDictionary<string, UnpublishableMessage>();
            _logger = logger;
            _cancellationToken = applicationLifetime.ApplicationStopping;
            _serviceName = nameof(UnpublishableMessageQueue);

            _isMonitorLoopStarted = 0;
        }

        public void StartMonitor()
        {
            var isMonitoring = Interlocked.CompareExchange(ref _isMonitorLoopStarted, 1, 0);

            if (isMonitoring == 1)
            {
                _logger.LogInformation($"Service '{_serviceName}' is already monitoring unpublishable messages.");
                return;
            }

            if (_cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation($"Service '{_serviceName}' is cancelled to before starting to monitor unpublishable messages.");
                return;
            }

            var monitorTask = Task.Factory.StartNew(Monitor, _cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            if (monitorTask.IsCanceled)
            {
                _logger.LogInformation($"Service '{_serviceName}' is cancelled to before starting to monitor unpublishable messages.");
            }
            else
            {
                _logger.LogInformation($"Service '{_serviceName}' starts to monitor unpublishable messages.");
            }
        }

        private void Monitor()
        {
            var wait = new ManualResetEventSlim(false);

            while(!_cancellationToken.IsCancellationRequested)
            {
                if (_unpublishableMessages.IsEmpty)
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

                if (!_unpublishableMessages.TryDequeue(out var unpublishableMessage))
                {
                    continue;
                }

                AddOrUpdateLatestUnpublishableMessage(unpublishableMessage);
            }

            _isMonitorLoopStarted = 0;
            _logger.LogInformation($"Service '{_serviceName}' is signaled to stop monitoring unpublishable messages.");
        }

        public void EnqueueUnpublishableMessage(UnpublishableMessage message)
        {
            _unpublishableMessages.Enqueue(message);
        }

        private void AddOrUpdateLatestUnpublishableMessage(UnpublishableMessage unpublishableMessage)
        {
            try
            {
                var key = GetHash($"{unpublishableMessage.Exchange}|{unpublishableMessage.ReplyCode}|{unpublishableMessage.ReplyText}|{unpublishableMessage.RoutingKey}");

                _latestUnpublishableMessages.AddOrUpdate(key, unpublishableMessage,
                    (k, v) =>
                    {
                        if (v.UtcTimestamp >= unpublishableMessage.UtcTimestamp)
                        {
                            return v;
                        }

                        v.BasicProperties = unpublishableMessage.BasicProperties;
                        v.Exchange = unpublishableMessage.Exchange;
                        v.ReplyCode = unpublishableMessage.ReplyCode;
                        v.ReplyText = unpublishableMessage.ReplyText;
                        v.RoutingKey = unpublishableMessage.RoutingKey;
                        v.UtcTimestamp = unpublishableMessage.UtcTimestamp;
                        return v;
                    });
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, $"Service '{_serviceName}' failed to add or update unpublishable message.");
            }
        }

        public ConcurrentDictionary<string, UnpublishableMessage> GetLatestUnpublishableMessages()
        {
            return _latestUnpublishableMessages;
        }

        public bool DeleteLatestUnpublishableMessage(string key)
        {
            return _latestUnpublishableMessages.TryRemove(key, out _);
        }

        private string GetHash(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] data = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));

                var builder = new StringBuilder();

                for (int i = 0; i < data.Length; i++)
                {
                    builder.Append(data[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }

    public class UnpublishableMessage
    {
        public IBasicProperties BasicProperties { get; set; }
        public string Exchange { get; set; }
        public ushort ReplyCode { get; set; }
        public string ReplyText { get; set; }
        public string RoutingKey { get; set; }
        public DateTime UtcTimestamp { get; set; }
    }
}
