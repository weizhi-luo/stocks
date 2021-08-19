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

        public static bool TryFindLineIndex(IList<string> lines, string targetLine, out int targetLineIndex)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                if (string.Equals(lines[i], targetLine))
                {
                    targetLineIndex = i;
                    return true;
                }
            }

            targetLineIndex = -1;
            return false;
        }

        public static void EnqueueServiceProcedureStatus(GrpcServiceProcedureStatusQueue grpcServiceProcedureStatusQueue, string serviceName,
            string procedureName, Status status, string detail)
        {
            grpcServiceProcedureStatusQueue.EnqueueServiceProcedureStatus(
                new GrpcServiceProcedureStatus
                {
                    ServiceProcedure = new GrpcServiceProcedure { Service = serviceName, Procedure = procedureName },
                    Status = status,
                    Detail = detail,
                    UtcTimestamp = DateTime.UtcNow
                });
        }

        public static void EnqueueDataToPublish(DataPublishQueue dataPublishQueue, string serviceName, string procedureName, string data)
        {
            dataPublishQueue.EnqueueDataToPublish(new DataToPublish
            {
                ServiceProcedure = new GrpcServiceProcedure
                {
                    Service = serviceName,
                    Procedure = procedureName
                },
                Data = data
            });
        }

        /// <summary>
        /// Download string from web
        /// </summary>
        /// <param name="httpClient"></param>
        /// <param name="httpRequestMessage"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>String content</returns>
        /// <exception cref="DataScrapeFailException">The request failed</exception>
        /// <exception cref="DataNotScrapedException">Download result is empty</exception>
        /// <exception cref="TaskCanceledException">The request is cancelled</exception>
        public static async Task<string> DownloadStringFromWebAsync(HttpClient httpClient, HttpRequestMessage httpRequestMessage, CancellationToken cancellationToken)
        {
            try
            {
                string content = null;

                using (var response = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    try
                    {
                        response.EnsureSuccessStatusCode();
                    }
                    catch (HttpRequestException exception)
                    {
                        throw new DataScrapeFailException(response.Content.ReadAsStringAsync().Result, exception);
                    }

                    content = await response.Content.ReadAsStringAsync(cancellationToken);
                }

                if (string.IsNullOrEmpty(content) || string.IsNullOrWhiteSpace(content))
                {
                    throw new DataNotScrapedException();
                }

                return content;
            }
            catch (ArgumentException exception)
            {
                throw new DataScrapeFailException("The request is null.", exception);
            }
            catch (InvalidOperationException exception)
            {
                throw new DataScrapeFailException("The request message was already sent by the HttpClient instance.", exception);
            }
            catch (HttpRequestException exception)
            {
                throw new DataScrapeFailException(
                    "The request failed due to an underlying issue such as network connectivity, DNS failure, server certificate validation or timeout.",
                    exception);
            }
            catch (TaskCanceledException exception)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                throw new DataScrapeFailException("The request failed due to timeout.", exception);
            }
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
