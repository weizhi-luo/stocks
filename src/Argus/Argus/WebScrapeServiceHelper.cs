using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Argus
{
    public static class WebScrapeServiceHelper
    {
        private static readonly DateTime UnixTimestampStart = new DateTime(1970, 1, 1);

        public static List<T> CreateGenericList<T>(params T[] elements)
        {
            return new List<T>(elements);
        }

        public static void HandleHttpRequestException(HttpRequestException exception, ILogger logger, GrpcServiceProcedureStatusQueue serviceProcedureStatusQueue, 
            string serviceName, string procedureName, HttpResponseMessage response)
        {
            var httpErrorContent = response.Content.ReadAsStringAsync().Result;
            var errorInformation = $"failed with unsucessful HTTP response: {httpErrorContent}";

            logger.LogError(exception, $"Service '{serviceName}' procedure '{procedureName}' {errorInformation}");
            serviceProcedureStatusQueue.EnqueueServiceProcedureStatus(
                new GrpcServiceProcedureStatus
                {
                    ServiceProcedure = new GrpcServiceProcedure { Service = serviceName, Procedure = procedureName },
                    Status = Status.Error,
                    Detail = $"{errorInformation}{Environment.NewLine}{exception}",
                    UtcTimestamp = DateTime.UtcNow
                });
        }

        public static void HandleScrapeException(Exception exception, ILogger logger, GrpcServiceProcedureStatusQueue serviceProcedureStatusQueue, 
            string serviceName, string procedureName, CancellationTokenSource cancellationTokenSource)
        {
            string errorInformation;

            if (exception is InvalidOperationException)
            {
                errorInformation = "failed due to uses a request which is already sent";
            }
            else if (exception is HttpRequestException)
            {
                errorInformation = "failed due to an underlying issue such as network connectivity, DNS failure, server certificate validation or timeout";
            }
            else if (exception is TaskCanceledException)
            {
                if (cancellationTokenSource.IsCancellationRequested)
                {
                    //logger.LogInformation($"Service '{serviceName}' procedure '{procedureName}' is cancelled.");
                    //serviceProcedureStatusQueue.EnqueueServiceProcedureStatus(
                    //    new GrpcServiceProcedureStatus
                    //    {
                    //        ServiceProcedure = new GrpcServiceProcedure { Service = serviceName, Procedure = procedureName },
                    //        Status = Status.Warning,
                    //        Detail = "cannelled",
                    //        UtcTimestamp = DateTime.UtcNow
                    //    });

                    return;
                }

                if (exception.InnerException is TimeoutException)
                {
                    errorInformation = "failed due to timeout";
                }
                else
                {
                    errorInformation = "failed";
                }
            }
            else
            {
                errorInformation = "failed";
            }

            var loggingErrorMessage = $"Service '{serviceName}' procedure '{procedureName}' {errorInformation}.";

            logger.LogError(exception, loggingErrorMessage);
            serviceProcedureStatusQueue.EnqueueServiceProcedureStatus(
                new GrpcServiceProcedureStatus
                {
                    ServiceProcedure = new GrpcServiceProcedure { Service = serviceName, Procedure = procedureName },
                    Status = Status.Error,
                    Detail = $"{errorInformation}{Environment.NewLine}{exception}",
                    UtcTimestamp = DateTime.UtcNow
                });
        }

        public static uint GetUnixTimeStamp(DateTime dateTime)
        {
            return Convert.ToUInt32((dateTime - UnixTimestampStart).TotalSeconds);
        }
    }
}
