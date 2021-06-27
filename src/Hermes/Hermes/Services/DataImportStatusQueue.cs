using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes
{
    public class DataImportStatusQueue
    {
        private readonly ConcurrentQueue<DataImportStatus> _dataImportStatuses;
        private readonly ConcurrentDictionary<GrpcServiceProcedure, DataImportStatus> _latestDataImportStatuses;
        private readonly ILogger<DataImportStatusQueue> _logger;
        private readonly CancellationToken _cancellationToken;
        private readonly string _serviceName;

        private uint _isMonitorLoopStarted;

        public DataImportStatusQueue(ILogger<DataImportStatusQueue> logger, IHostApplicationLifetime applicationLifetime)
        {
            _dataImportStatuses = new ConcurrentQueue<DataImportStatus>();
            _latestDataImportStatuses = new ConcurrentDictionary<GrpcServiceProcedure, DataImportStatus>();
            _logger = logger;
            _cancellationToken = applicationLifetime.ApplicationStopping;
            _serviceName = nameof(DataImportStatusQueue);

            _isMonitorLoopStarted = 0;
        }

        public void StartMonitors()
        {
            var isMonitoring = Interlocked.CompareExchange(ref _isMonitorLoopStarted, 1, 0);

            if (isMonitoring == 1)
            {
                _logger.LogInformation($"Service '{_serviceName}' is already monitoring data import statuses.");
                return;
            }

            if (_cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation($"Service '{_serviceName}' is cancelled to before starting to monitor data import statuses.");
                return;
            }

            var monitorTask = Task.Factory.StartNew(Monitor, _cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            if (monitorTask.IsCanceled)
            {
                _logger.LogInformation($"Service '{_serviceName}' is cancelled to before starting to monitor data import statuses.");
            }
            else
            {
                _logger.LogInformation($"Service '{_serviceName}' starts to monitor data import statuses.");
            }
        }

        private void Monitor()
        {
            var wait = new ManualResetEventSlim(false);

            while (!_cancellationToken.IsCancellationRequested)
            {
                if (_dataImportStatuses.IsEmpty)
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

                if (!_dataImportStatuses.TryDequeue(out var serviceProcedureStatus))
                {
                    continue;
                }

                try
                {
                    AddOrUpdateLatestDataImportStatus(serviceProcedureStatus);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, $"Service '{_serviceName}' failed to add or update latest data import status.");
                }
            }

            _isMonitorLoopStarted = 0;
            _logger.LogInformation($"Service '{_serviceName}' is signaled to stop monitoring .");
        }

        public void EnqueueDataImportStatus(DataImportStatus dataImportStatus)
        {
            _dataImportStatuses.Enqueue(dataImportStatus);
        }

        private void AddOrUpdateLatestDataImportStatus(DataImportStatus dataImportStatus)
        {
            _latestDataImportStatuses.AddOrUpdate(dataImportStatus.DataScrapeServiceProcedure, dataImportStatus,
                (k, v) =>
                {
                    if (v.UtcTimestamp >= dataImportStatus.UtcTimestamp)
                    {
                        return v;
                    }

                    v.Status = dataImportStatus.Status;
                    v.Detail = dataImportStatus.Detail;
                    v.UtcTimestamp = dataImportStatus.UtcTimestamp;
                    return v;
                });
        }

        public ICollection<DataImportStatus> GetLatestDataImportStatuses()
        {
            return _latestDataImportStatuses.Values;
        }

        public List<DataImportStatus> GetLatestDataImportStatusesSuccess()
        {
            return _latestDataImportStatuses.Values.Where(x => x.Status == Status.Success).ToList();
        }

        public List<DataImportStatus> GetLatestDataImportStatusesWarning()
        {
            return _latestDataImportStatuses.Values.Where(x => x.Status == Status.Warning).ToList();
        }

        public List<DataImportStatus> GetLatestDataImportStatusesError()
        {
            return _latestDataImportStatuses.Values.Where(x => x.Status == Status.Error).ToList();
        }
    }
}
