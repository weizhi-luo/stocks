using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Argus.HealthChecks
{
    public class DataPublishHealthCheck : IHealthCheck
    {
        private DataPublishQueue _dataPublishQueue;
        private UnpublishableMessageQueue _unpublishableMessageQueue;

        public DataPublishHealthCheck(DataPublishQueue dataPublishQueue, UnpublishableMessageQueue unpublishableMessageQueue)
        {
            _dataPublishQueue = dataPublishQueue;
            _unpublishableMessageQueue = unpublishableMessageQueue;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var errors = _dataPublishQueue.GetLatestDataPublishErrors();
            var unpublishableMessages = _unpublishableMessageQueue.GetLatestUnpublishableMessages();

            if (!errors.Any() && !unpublishableMessages.Any())
            {
                return Task.FromResult(HealthCheckResult.Healthy("No publish errors"));
            }

            var errorsDetail = new StringBuilder();
            if (errors.Any())
            {
                errorsDetail.AppendLine("Data publish error(s):");
                foreach (var error in errors)
                {
                    errorsDetail.AppendLine($"\tService:{error.ServiceProcedure.Service}|Procedure:{error.ServiceProcedure.Procedure}|Error:{error.Error}");
                }
            }
            if (unpublishableMessages.Any())
            {
                errorsDetail.AppendLine("Unpublishable message(s)");
                foreach (var message in unpublishableMessages)
                {
                    errorsDetail.AppendLine($"\tKey:{message.Key}|Exchange:{message.Value.Exchange}|ReplyCode:{message.Value.ReplyCode}|ReplyText:{message.Value.ReplyText}|RoutingKey:{message.Value.RoutingKey}|UtcTimestamp:{message.Value.UtcTimestamp:yyyy-MM-dd HH:mm:ss}");
                }
            }

            return Task.FromResult(HealthCheckResult.Unhealthy($"Errors in publishing data:{Environment.NewLine}{errorsDetail}"));
        }
    }
}
