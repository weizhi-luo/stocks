using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Argus
{
    public class GrpcServiceProcedureStatusQueueMonitor : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly GrpcServiceProcedureStatusQueue _serviceProcedureStatusQueue;
        private readonly string _serviceName;
        
        public GrpcServiceProcedureStatusQueueMonitor(ILogger<GrpcServiceProcedureStatusQueueMonitor> logger, 
            GrpcServiceProcedureStatusQueue serviceProcedureStatusQueue, IConfiguration configuration)
        {
            _logger = logger;
            _serviceProcedureStatusQueue = serviceProcedureStatusQueue;
            _serviceName = GetType().Name;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"Service '{_serviceName}' is started.");

            var monitorServiceProcedureStatusQueueTask = Task.Factory.StartNew(MonitorServiceProcedureStatusQueue, stoppingToken, TaskCreationOptions.LongRunning);
            await monitorServiceProcedureStatusQueueTask;

            _logger.LogInformation($"Service '{_serviceName}' is signaled to stop.");
        }

        private void MonitorServiceProcedureStatusQueue(object cancellationTokenObject)
        {
            var cancellationToken = (CancellationToken)cancellationTokenObject;
            var wait = new ManualResetEventSlim(false);
            var methodName = nameof(MonitorServiceProcedureStatusQueue);

            while (!cancellationToken.IsCancellationRequested)
            {
                var serviceProcedureStatus = _serviceProcedureStatusQueue.DequeueServiceProcedureStatus();
                if (serviceProcedureStatus == null)
                {
                    wait.Wait(1000);
                    continue;
                }

                try
                {
                    _serviceProcedureStatusQueue.AddOrUpdateLatestServiceProcedureStatus(serviceProcedureStatus);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, $"Service '{_serviceName}' method '{methodName}' failed.");
                }
            }
        }
    }
}
