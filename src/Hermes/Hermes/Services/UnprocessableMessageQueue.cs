using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes
{
    public class UnprocessableMessageQueue
    {
        private readonly ConcurrentQueue<UnprocessableMessage> _unprocessableMessages;
        private readonly ConcurrentDictionary<string, UnprocessableMessage> _latestUnprocessableMessages;
        private readonly ILogger<UnprocessableMessageQueue> _logger;
        private readonly CancellationToken _cancellationToken;
        private readonly string _serviceName;

        private uint _isMonitorLoopStarted;

        public UnprocessableMessageQueue(ILogger<UnprocessableMessageQueue> logger, IHostApplicationLifetime applicationLifetime)
        {
            _unprocessableMessages = new ConcurrentQueue<UnprocessableMessage>();
            _latestUnprocessableMessages = new ConcurrentDictionary<string, UnprocessableMessage>();
            _logger = logger;
            _cancellationToken = applicationLifetime.ApplicationStopping;
            _serviceName = nameof(UnprocessableMessageQueue);

            _isMonitorLoopStarted = 0;
        }

        public void StartMonitor()
        {
            var isMonitoring = Interlocked.CompareExchange(ref _isMonitorLoopStarted, 1, 0);

            if (isMonitoring == 1)
            {
                _logger.LogInformation($"Service '{_serviceName}' is already monitoring unprocessable messages.");
                return;
            }

            if (_cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation($"Service '{_serviceName}' is cancelled to before starting to monitor unprocessable messages.");
                return;
            }

            var monitorTask = Task.Factory.StartNew(Monitor, _cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            if (monitorTask.IsCanceled)
            {
                _logger.LogInformation($"Service '{_serviceName}' is cancelled to before starting to monitor unprocessable messages.");
            }
            else
            {
                _logger.LogInformation($"Service '{_serviceName}' starts to monitor unprocessable messages.");
            }
        }

        private void Monitor()
        {
            var wait = new ManualResetEventSlim(false);

            while (!_cancellationToken.IsCancellationRequested)
            {
                if (_unprocessableMessages.IsEmpty)
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

                if (!_unprocessableMessages.TryDequeue(out var unprocessableMessage))
                {
                    continue;
                }

                try
                {
                    AddOrUpdateLatestUnprocessableMessage(unprocessableMessage);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, $"Service '{_serviceName}' failed to add or update unprocessable message.");
                }
            }

            _isMonitorLoopStarted = 0;
            _logger.LogInformation($"Service '{_serviceName}' is signaled to stop monitoring unprocessable messages.");
        }

        public void EnqueueUnprocessableMessage(UnprocessableMessage message)
        {
            _unprocessableMessages.Enqueue(message);
        }

        private void AddOrUpdateLatestUnprocessableMessage(UnprocessableMessage unprocessableMessage)
        {
            string key;
            using (SHA256 sha256 = SHA256.Create())
            {
                key = GetHash(sha256, $"{unprocessableMessage.ConsumerTag}|{unprocessableMessage.DeliveryTag}|{unprocessableMessage.Redelivered}|{unprocessableMessage.Exchange}|{unprocessableMessage.RoutingKey}");
            }

            _latestUnprocessableMessages.AddOrUpdate(key, unprocessableMessage,
                (k, v) =>
                {
                    if (v.UtcTimestamp >= unprocessableMessage.UtcTimestamp)
                    {
                        return v;
                    }

                    v.ConsumerTag = unprocessableMessage.ConsumerTag;
                    v.DeliveryTag = unprocessableMessage.DeliveryTag;
                    v.Redelivered = unprocessableMessage.Redelivered;
                    v.Exchange = unprocessableMessage.Exchange;
                    v.RoutingKey = unprocessableMessage.RoutingKey;
                    v.BasicProperties = unprocessableMessage.BasicProperties;
                    v.Detail = unprocessableMessage.Detail;
                    v.UtcTimestamp = unprocessableMessage.UtcTimestamp;
                    return v;
                });
        }

        public ConcurrentDictionary<string, UnprocessableMessage> GetLatestUnprocessableMessages()
        {
            return _latestUnprocessableMessages;
        }

        public bool DeleteLatestUnprocessableMessage(string key)
        {
            return _latestUnprocessableMessages.TryRemove(key, out _);
        }

        private string GetHash(HashAlgorithm hashAlgorithm, string input)
        {
            byte[] data = hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(input));

            var builder = new StringBuilder();

            for (int i = 0; i < data.Length; i++)
            {
                builder.Append(data[i].ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
