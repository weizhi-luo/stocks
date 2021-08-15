using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Argus
{
    public class GrpcServiceProcedureStatusQueue
    {
        private readonly ConcurrentQueue<GrpcServiceProcedureStatus> _serviceProcedureStatuses;
        private readonly ConcurrentDictionary<GrpcServiceProcedure, GrpcServiceProcedureStatus> _latestServiceProcedureStatuses;
        private readonly ILogger<GrpcServiceProcedureStatusQueue> _logger;
        private readonly CancellationToken _cancellationToken;
        private readonly string _serviceName;

        private uint _isMonitorLoopStarted;

        public GrpcServiceProcedureStatusQueue(ILogger<GrpcServiceProcedureStatusQueue> logger, IHostApplicationLifetime applicationLifetime)
        {
            _serviceProcedureStatuses = new ConcurrentQueue<GrpcServiceProcedureStatus>();
            _latestServiceProcedureStatuses = new ConcurrentDictionary<GrpcServiceProcedure, GrpcServiceProcedureStatus>();
            _logger = logger;
            _cancellationToken = applicationLifetime.ApplicationStopping;
            _serviceName = nameof(GrpcServiceProcedureStatusQueue);

            _isMonitorLoopStarted = 0;
        }

        public void StartMonitor()
        {
            var isMonitoring = Interlocked.CompareExchange(ref _isMonitorLoopStarted, 1, 0);
            if (isMonitoring == 1)
            {
                _logger.LogInformation($"Service '{_serviceName}' is already monitoring status.");
                return;
            }

            if (_cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation($"Service '{_serviceName}' is cancelled to before starting to monitor status.");
                return;
            }

            var monitorTask = Task.Factory.StartNew(Monitor, _cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            if (monitorTask.IsCanceled)
            {
                _logger.LogInformation($"Service '{_serviceName}' is cancelled to before starting to monitor status.");
            }
            else
            {
                _logger.LogInformation($"Service '{_serviceName}' starts to monitor status.");
            }
        }

        private void Monitor()
        {
            var wait = new ManualResetEventSlim(false);

            while (!_cancellationToken.IsCancellationRequested)
            {
                if (_serviceProcedureStatuses.IsEmpty)
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

                if (!_serviceProcedureStatuses.TryDequeue(out var serviceProcedureStatus))
                {
                    continue;
                }

                AddOrUpdateLatestServiceProcedureStatus(serviceProcedureStatus);
            }

            _isMonitorLoopStarted = 0;
            _logger.LogInformation($"Service '{_serviceName}' is signaled to stop monitoring status.");
        }

        public void EnqueueServiceProcedureStatus(GrpcServiceProcedureStatus serviceProcedureStatus)
        {
            _serviceProcedureStatuses.Enqueue(serviceProcedureStatus);
        }

        private void AddOrUpdateLatestServiceProcedureStatus(GrpcServiceProcedureStatus serviceProcedureStatus)
        {
            try
            {
                _latestServiceProcedureStatuses.AddOrUpdate(serviceProcedureStatus.ServiceProcedure, serviceProcedureStatus,
                    (k, v) => {
                        if (v.UtcTimestamp >= serviceProcedureStatus.UtcTimestamp)
                        {
                            return v;
                        }

                        v.Status = serviceProcedureStatus.Status;
                        v.Detail = serviceProcedureStatus.Detail;
                        v.UtcTimestamp = serviceProcedureStatus.UtcTimestamp;
                        return v;
                    });
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, $"Service '{_serviceName}' failed to add or update latest service procedure status.");
            }
        }

        public ICollection<GrpcServiceProcedureStatus> GetLatestServiceProcedureStatuses()
        {
            return _latestServiceProcedureStatuses.Values;
        }

        public List<GrpcServiceProcedureStatus> GetLatestServiceProcedureStatusesSuccess()
        {
            return _latestServiceProcedureStatuses.Values.Where(x => x.Status == Status.Success).ToList();
        }

        public List<GrpcServiceProcedureStatus> GetLatestServiceProcedureStatusesWarning()
        {
            return _latestServiceProcedureStatuses.Values.Where(x => x.Status == Status.Warning).ToList();
        }

        public List<GrpcServiceProcedureStatus> GetLatestServiceProcedureStatusesError()
        {
            return _latestServiceProcedureStatuses.Values.Where(x => x.Status == Status.Error).ToList();
        }
    }
}
