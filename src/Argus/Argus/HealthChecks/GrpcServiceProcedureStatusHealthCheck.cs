using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;
using System.Linq;
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

            var errors = new List<GrpcServiceProcedureStatus>();
            var warnings = new List<GrpcServiceProcedureStatus>();

            foreach(var status in statuses)
            {
                switch (status.Status)
                {
                    case Status.Error:
                        errors.Add(status);
                        break;
                    case Status.Warning:
                        warnings.Add(status);
                        break;
                    default:
                        break;
                }
            }

            if (errors.Any())
            {
                return Task.FromResult(
                    HealthCheckResult.Unhealthy($"GRPC services/procedures ran with errors:{Environment.NewLine}{string.Join(Environment.NewLine, errors.Select(x => "\t" + x.Detail))}"));
            }

            if (warnings.Any())
            {
                return Task.FromResult(
                    HealthCheckResult.Degraded($"GRPC services/procedures ran with warnings:{Environment.NewLine}{string.Join(Environment.NewLine, warnings.Select(x => "\t" + x.Detail))}"));
            }

            return Task.FromResult(HealthCheckResult.Healthy("GRPC services/procedures ran without errors."));
        }
    }
}
