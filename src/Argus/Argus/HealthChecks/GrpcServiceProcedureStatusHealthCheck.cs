using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Argus.HealthChecks
{
    public class GrpcServiceProcedureStatusHealthCheck : IHealthCheck
    {
        private GrpcServiceProcedureStatusQueue _serviceProcedureStatusQueue;

        public GrpcServiceProcedureStatusHealthCheck(GrpcServiceProcedureStatusQueue serviceProcedureStatusQueue)
        {
            _serviceProcedureStatusQueue = serviceProcedureStatusQueue;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var statuses = _serviceProcedureStatusQueue.GetLatestServiceProcedureStatuses();

            if (!statuses.Any())
            {
                return Task.FromResult(HealthCheckResult.Healthy("No GRPC service/procedure is called yet."));
            }

            var (errorMessage, warningMessage) = GenerateErrorAndWarningMessage(statuses);

            if (!string.IsNullOrEmpty(errorMessage))
            {
                return Task.FromResult(
                    HealthCheckResult.Unhealthy($"GRPC services/procedures ran with errors:{Environment.NewLine}{errorMessage}"));
            }

            if (!string.IsNullOrEmpty(warningMessage))
            {
                return Task.FromResult(
                    HealthCheckResult.Degraded($"GRPC services/procedures ran with warnings:{Environment.NewLine}{warningMessage}"));
            }

            return Task.FromResult(HealthCheckResult.Healthy("GRPC services/procedures ran without errors."));
        }

        private (string ErrorMessage, string WarningMessage) GenerateErrorAndWarningMessage(ICollection<GrpcServiceProcedureStatus> statuses)
        {
            var errorMessageBuilder = new StringBuilder();
            var warningMessageBuilder = new StringBuilder();

            foreach (var status in statuses)
            {
                switch (status.Status)
                {
                    case Status.Error:
                        errorMessageBuilder.AppendLine("\t" + status.Detail);
                        break;
                    case Status.Warning:
                        warningMessageBuilder.AppendLine("\t" + status.Detail);
                        break;
                    default:
                        break;
                }
            }

            return (errorMessageBuilder.ToString(), warningMessageBuilder.ToString());
        }
    }
}
