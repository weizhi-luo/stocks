using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace Argus
{
    public static class ScrapeServiceHelper
    {
        private static readonly DateTime UnixTimestampStart = new DateTime(1970, 1, 1);

        public static List<T> CreateGenericList<T>(params T[] elements)
        {
            return new List<T>(elements);
        }

        public static void HandleHttpRequestException(HttpRequestException exception, ILogger logger, GrpcServiceProcedureStatusQueue serviceProcedureStatusQueue, 
            string serviceName, string procedureName, string responseMessage)
        {
            var errorInformation = $"failed with unsucessful HTTP response: {responseMessage}";

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
            if (exception is TaskCanceledException && cancellationTokenSource.IsCancellationRequested)
                return;

            var errorDetail = GenerateScrapeErrorDetail(exception);

            var loggingErrorMessage = $"Service '{serviceName}' procedure '{procedureName}' {errorDetail}.";

            logger.LogError(exception, loggingErrorMessage);
            serviceProcedureStatusQueue.EnqueueServiceProcedureStatus(
                new GrpcServiceProcedureStatus
                {
                    ServiceProcedure = new GrpcServiceProcedure { Service = serviceName, Procedure = procedureName },
                    Status = Status.Error,
                    Detail = $"{errorDetail}{Environment.NewLine}{exception}",
                    UtcTimestamp = DateTime.UtcNow
                });
        }

        private static string GenerateScrapeErrorDetail(Exception exception)
        {
            if (exception is InvalidOperationException)
            {
                return "failed due to uses a request which is already sent";
            }
            
            if (exception is HttpRequestException)
            {
                return "failed due to an underlying issue such as network connectivity, DNS failure, server certificate validation or timeout";
            }
            
            if (exception is TaskCanceledException && exception.InnerException is TimeoutException)
            {
                return "failed due to timeout";
            }

            return "failed";
        }

        public static uint GetUnixTimeStamp(DateTime dateTime)
        {
            return Convert.ToUInt32((dateTime - UnixTimestampStart).TotalSeconds);
        }

        /// <summary>
        /// Download file content from FTP
        /// </summary>
        /// <param name="ftpFilePath">Path to the file on FTP</param>
        /// <param name="username">FTP username</param>
        /// <param name="password">FTP password</param>
        /// <returns>File content</returns>
        /// <exception cref="DataScrapeFailException">Data scrape operation fails.</exception>
        /// <exception cref="DataNotScrapedException">Scraping result is empty.</exception>
        public async static Task<string> DownloadFtpFileContentAsync(string ftpFilePath, string username, string password)
        {
            var ftpRequest = CreateFtpDownloadRequest(ftpFilePath, username, password);
            var fileContent = await DownloadFtpFileContentAsync(ftpRequest);
            
            return fileContent;
        }

        private static FtpWebRequest CreateFtpDownloadRequest(string ftpFilePath, string username, string password)
        {
            FtpWebRequest ftpRequest;
            try
            {
                ftpRequest = (FtpWebRequest)WebRequest.Create(ftpFilePath);
                ftpRequest.Method = WebRequestMethods.Ftp.DownloadFile;
                ftpRequest.Credentials = new NetworkCredential(username, password);
            }
            catch (NotSupportedException exception)
            {
                throw new DataScrapeFailException($"failed to download file content from {ftpFilePath} due to registered URI scheme", exception);
            }
            catch (ArgumentNullException exception)
            {
                throw new DataScrapeFailException($"failed to download file content from {ftpFilePath} due to null URI", exception);
            }
            catch (SecurityException exception)
            {
                throw new DataScrapeFailException(
                    $"failed to download file content from {ftpFilePath} due to lack of WebPermissionAttribute permission to connect to the request URI or a URI that the request is redirect to",
                    exception);
            }
            catch (UriFormatException exception)
            {
                throw new DataScrapeFailException($"failed to download file content from {ftpFilePath} due to invalid URI", exception);
            }
            catch (Exception exception)
            {
                throw new DataScrapeFailException("failed", exception);
            }

            return ftpRequest;
        }

        private async static Task<string> DownloadFtpFileContentAsync(FtpWebRequest ftpRequest)
        {
            string fileContent;

            try
            {
                using (var response = await ftpRequest.GetResponseAsync())
                {
                    using (var responseStream = response.GetResponseStream())
                    {
                        using (var reader = new StreamReader(responseStream))
                        {
                            fileContent = await reader.ReadToEndAsync();
                        }
                    }
                }
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw new DataScrapeFailException($"failed to download file content from {ftpRequest.RequestUri} due to the number of characters exceeding {int.MaxValue}", exception);
            }
            catch (ObjectDisposedException exception)
            {
                throw new DataScrapeFailException($"failed to download file content from {ftpRequest.RequestUri} due to disposed stream", exception);
            }
            catch (InvalidOperationException exception)
            {
                throw new DataScrapeFailException($"failed to download file content from {ftpRequest.RequestUri} due to stream reader being used", exception);
            }
            catch (ArgumentNullException exception)
            {
                throw new DataScrapeFailException($"failed to download file content from {ftpRequest.RequestUri} due to null stream", exception);
            }
            catch (ArgumentException exception)
            {
                throw new DataScrapeFailException($"failed to download file content from {ftpRequest.RequestUri} due to unreadable stream", exception);
            }
            catch (Exception exception)
            {
                throw new DataScrapeFailException("failed", exception);
            }

            if (string.IsNullOrEmpty(fileContent) || string.IsNullOrWhiteSpace(fileContent))
            {
                throw new DataNotScrapedException();
            }

            return fileContent;
        }
    }
}
