using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes.HealthChecks
{
    public class DataImportStatusHealthCheck : IHealthCheck
    {
        private DataImportStatusQueue _dataImportStatusQueue;
        private UnprocessableMessageQueue _unprocessableMessageQueue;

        public DataImportStatusHealthCheck(DataImportStatusQueue dataImportStatusQueue, UnprocessableMessageQueue unprocessableMessageQueue)
        {
            _dataImportStatusQueue = dataImportStatusQueue;
            _unprocessableMessageQueue = unprocessableMessageQueue;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var statuses = _dataImportStatusQueue.GetLatestDataImportStatuses();
            var unprocessableMessages = _unprocessableMessageQueue.GetLatestUnprocessableMessages();

            var importErrors = new List<DataImportStatus>();
            var importWarnings = new List<DataImportStatus>();

            foreach (var status in statuses)
            {
                switch (status.Status)
                {
                    case Status.Error:
                        importErrors.Add(status);
                        break;
                    case Status.Warning:
                        importWarnings.Add(status);
                        break;
                    default:
                        break;
                }
            }

            if (!importErrors.Any() && !importWarnings.Any() && !unprocessableMessages.Any())
            {
                return Task.FromResult(HealthCheckResult.Healthy("No import issues"));
            }

            var issuesDetail = new StringBuilder();

            if (importErrors.Any())
            {
                issuesDetail.AppendLine("Data import error(s):");
                foreach (var importError in importErrors)
                {
                    issuesDetail.AppendLine($"\tSource service:{importError.DataScrapeServiceProcedure.Service}|Source procedure:{importError.DataScrapeServiceProcedure.Procedure}|Error:{importError.Detail}");
                }
            }

            if (importWarnings.Any())
            {
                issuesDetail.AppendLine("Data import warning(s):");
                foreach (var importWarning in importWarnings)
                {
                    issuesDetail.AppendLine($"\tSource service:{importWarning.DataScrapeServiceProcedure.Service}|Source procedure:{importWarning.DataScrapeServiceProcedure.Procedure}|Warning:{importWarning.Detail}");
                }
            }

            if (unprocessableMessages.Any())
            {
                issuesDetail.AppendLine("Unprocessable message(s):");
                foreach (var message in unprocessableMessages)
                {
                    issuesDetail.AppendLine(
                        $"\tKey:{message.Key}|ConsumerTag:{message.Value.ConsumerTag}|DeliveryTag:{message.Value.DeliveryTag}|Redelivered:{message.Value.Redelivered}|Exchange:{message.Value.Exchange}|RoutingKey:{message.Value.RoutingKey}|Detail:{message.Value.Detail}|UtcTimestamp:{message.Value.UtcTimestamp:yyyy-MM-dd HH:mm:ss}");
                }
            }

            return Task.FromResult(HealthCheckResult.Unhealthy($"Issues in importing data:{Environment.NewLine}{issuesDetail}"));
        }
    }
}
