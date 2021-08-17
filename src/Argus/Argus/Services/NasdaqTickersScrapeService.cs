using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Argus
{
    public class NasdaqTickersScrapeService : NasdaqTickersScraper.NasdaqTickersScraperBase, IDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private readonly ILogger<NasdaqTickersScrapeService> _logger;
        private readonly DataPublishQueue _dataPublishQueue;
        private readonly GrpcServiceProcedureStatusQueue _serviceProcedureStatusQueue;
        private readonly CancellationToken _cancellationToken;
        private readonly string _serviceName;
       
        private uint _isScrapingNasdaqListed;
        private Task _nasdaqListedScrapeTask;
        private uint _isScrapingOtherListed;
        private Task _otherListedScrapeTask;

        public NasdaqTickersScrapeService(ILogger<NasdaqTickersScrapeService> logger, DataPublishQueue dataPublishQueue, GrpcServiceProcedureStatusQueue serviceProcedureStatusQueue)
        {
            _logger = logger;
            _dataPublishQueue = dataPublishQueue;
            _serviceProcedureStatusQueue = serviceProcedureStatusQueue;

            _cancellationToken = _cancellationTokenSource.Token;
            _serviceName = GetType().Name;

            _isScrapingNasdaqListed = 0;
            _nasdaqListedScrapeTask = Task.CompletedTask;

            _isScrapingOtherListed = 0;
            _otherListedScrapeTask = Task.CompletedTask;
        }

        public override Task<ScrapeStatusReply> ScrapeNasdaqListed(Empty request, ServerCallContext context)
        {
            var procedureName = nameof(ScrapeNasdaqListed);

            _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' is called.");

            var isRunning = Interlocked.CompareExchange(ref _isScrapingNasdaqListed, 1, 0);
            if (isRunning == 1)
            {
                _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' is already running.");
                return Task.FromResult(new ScrapeStatusReply { Message = "NasdaqListed is being scraped" });
            }

            _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' starts to scrape NasdaqListed.");

            _nasdaqListedScrapeTask = ScrapeNasdaqListed();
            return Task.FromResult(new ScrapeStatusReply { Message = "starts to scrape NasdaqListed" });
        }

        public override Task<ScrapeStatusReply> ScrapeOtherListed(Empty request, ServerCallContext context)
        {
            var procedureName = nameof(ScrapeOtherListed);

            _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' is called.");

            var isRunning = Interlocked.CompareExchange(ref _isScrapingOtherListed, 1, 0);
            if (isRunning == 1)
            {
                _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' is already running.");
                return Task.FromResult(new ScrapeStatusReply { Message = "NasdaqOtherListed is being scraped" });
            }

            _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' starts to scrape NasdaqOtherListed.");

            _otherListedScrapeTask = ScrapeNasdaqOtherListed();
            return Task.FromResult(new ScrapeStatusReply { Message = "starts to scrape NasdaqOtherListed" });
        }

        private async Task ScrapeNasdaqListed()
        {
            const string FtpFilePath = "ftp://ftp.nasdaqtrader.com/symboldirectory/nasdaqlisted.txt";
            var procedureName = nameof(ScrapeNasdaqListed);

            try
            {
                await ScrapeNasdaqData(procedureName, FtpFilePath, ProcessAndConvertNasdaqListedToJson);
            }
            finally
            {
                _isScrapingNasdaqListed = 0;
            }
        }

        private async Task ScrapeNasdaqOtherListed()
        {
            const string FtpFilePath = "ftp://ftp.nasdaqtrader.com/symboldirectory/otherlisted.txt";
            var procedureName = nameof(ScrapeNasdaqOtherListed);
            
            try
            {
                await ScrapeNasdaqData(procedureName, FtpFilePath, ProcessAndConvertNasdaqOtherListedToJson);
            }
            finally
            {
                _isScrapingOtherListed = 0;
            }
        }

        private async Task ScrapeNasdaqData(string procedureName, string ftpFilePath, Func<string, string> ProcessAndConvertNasdaqDataToJson)
        {
            EnqueueServiceProcedureStatus(procedureName, Status.Information, $"Service '{_serviceName}' procedure '{procedureName}' is scraping data.");

            string fileContent;

            try
            {
                fileContent = await ScrapeServiceHelper.DownloadFtpFileContentAsync(ftpFilePath, "anonymous", "");
            }
            catch (DataScrapeFailException exception)
            {
                _logger.LogError(exception, $"Service '{_serviceName}' procedure '{procedureName}' failed.");
                EnqueueServiceProcedureStatus(procedureName, Status.Error, $"failed{Environment.NewLine}{exception}");
                
                return;
            }
            catch (DataNotScrapedException)
            {
                _logger.LogWarning($"Service '{_serviceName}' procedure '{procedureName}' did not scrape any data.");
                EnqueueServiceProcedureStatus(procedureName, Status.Warning, "did not scrape any data");

                return;
            }

            try
            {
                string nasdaqData;

                try
                {
                    nasdaqData = ProcessAndConvertNasdaqDataToJson(fileContent);
                }
                catch (CancellationRequestedException)
                {
                    return;
                }
                catch (DataNotScrapedException)
                {
                    _logger.LogWarning($"Service '{_serviceName}' procedure '{procedureName}' did not scrape any data.");
                    EnqueueServiceProcedureStatus(procedureName, Status.Warning, "did not scrape any data");
                    
                    return;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, $"Service '{_serviceName}' procedure '{procedureName}' failed.");
                    EnqueueServiceProcedureStatus(procedureName, Status.Error, $"failed{Environment.NewLine}{exception}");

                    return;
                }
                
                if (_cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                EnqueueDataToPublish(procedureName, nasdaqData);
                EnqueueServiceProcedureStatus(procedureName, Status.Success, $"Service '{_serviceName}' procedure '{procedureName}' finished scraping data.");

                _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' finished scraping data.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, $"Service '{_serviceName}' procedure '{procedureName}' failed.");
                EnqueueServiceProcedureStatus(procedureName, Status.Error, $"failed{Environment.NewLine}{exception}");
            }
        }

        /// <summary>
        /// Process other listed tickers data and convert to Json
        /// </summary>
        /// <param name="fileContent">File content on Nasdaq FTP for other listed tickers</param>
        /// <returns>Other listed tickers data converted to Json</returns>
        /// <exception cref="CancellationRequestedException">Cancellation is requested.</exception>
        /// <exception cref="InvalidDataException">Data content is invalid.</exception>
        /// <exception cref="DataNotScrapedException">Scraping result is empty.</exception>
        /// <exception cref="DataProcessFailException">The data process operation failed due to unexpected data.</exception>
        private string ProcessAndConvertNasdaqOtherListedToJson(string fileContent)
        {
            const string NasdaqOtherListedFileColumnNamesLine = "ACT Symbol|Security Name|Exchange|CQS Symbol|ETF|Round Lot Size|Test Issue|NASDAQ Symbol";
            const string TickerColumnName = "ACT Symbol";
            const string NameColumnName = "Security Name";
            const string ExchangeColumnName = "Exchange";
            const string CqsSymbolColumnName = "CQS Symbol";
            const string EtfColumnName = "ETF";
            const string RoundLotSizeColumnName = "Round Lot Size";
            const string TestIssueColumnName = "Test Issue";
            const string NasdaqSymbolColumnName = "NASDAQ Symbol";

            var scrapeTimestampUtc = DateTime.UtcNow;

            var lines = fileContent.Split(Environment.NewLine);

            if (!TryFindColumnNamesLineIndex(lines, NasdaqOtherListedFileColumnNamesLine, out var columnNamesLineIndex))
            {
                throw new InvalidDataException("Failed to extract column names line.");
            }

            var columnNames = lines[columnNamesLineIndex].Split("|");
            var tickerIndex = -1;
            var nameIndex = -1;
            var exchangeIndex = -1;
            var cqsSymbolIndex = -1;
            var etfIndex = -1;
            var roundLotSizeIndex = -1;
            var testIssueIndex = -1;
            var nasdaqSymbolIndex = -1;

            for (var i = 0; i < columnNames.Length; i++)
            {
                var columnName = columnNames[i];

                switch (columnName)
                {
                    case TickerColumnName:
                        tickerIndex = i;
                        break;
                    case NameColumnName:
                        nameIndex = i;
                        break;
                    case ExchangeColumnName:
                        exchangeIndex = i;
                        break;
                    case CqsSymbolColumnName:
                        cqsSymbolIndex = i;
                        break;
                    case EtfColumnName:
                        etfIndex = i;
                        break;
                    case RoundLotSizeColumnName:
                        roundLotSizeIndex = i;
                        break;
                    case TestIssueColumnName:
                        testIssueIndex = i;
                        break;
                    case NasdaqSymbolColumnName:
                        nasdaqSymbolIndex = i;
                        break;
                    default:
                        break;
                }
            }

            if (tickerIndex == -1 || nameIndex == -1 || exchangeIndex == -1 || cqsSymbolIndex == -1 ||
                etfIndex == -1 || roundLotSizeIndex == -1 || testIssueIndex == -1 || nasdaqSymbolIndex == -1)
            {
                throw new InvalidDataException("Failed to extract indexes for required column(s).");
            }

            var tickerList = ScrapeServiceHelper.CreateGenericList(lines.Skip(columnNamesLineIndex + 1)
                .Where(x => !string.IsNullOrEmpty(x) && !string.IsNullOrWhiteSpace(x) && !x.StartsWith("File Creation Time:", StringComparison.OrdinalIgnoreCase))
                .Select(x =>
                {
                    var lineElements = x.Split("|");

                    var ticker = lineElements[tickerIndex].Trim();
                    var name = lineElements[nameIndex].Trim();
                    var exchange = lineElements[exchangeIndex].Trim();
                    var cqsSymbol = lineElements[cqsSymbolIndex].Trim();

                    bool? etf;
                    if (string.Equals(lineElements[etfIndex].Trim(), "N", StringComparison.OrdinalIgnoreCase))
                    {
                        etf = false;
                    }
                    else if (string.Equals(lineElements[etfIndex].Trim(), "Y", StringComparison.OrdinalIgnoreCase))
                    {
                        etf = true;
                    }
                    else if (string.IsNullOrEmpty(lineElements[etfIndex].Trim()))
                    {
                        etf = null;
                    }
                    else
                    {
                        throw new DataProcessFailException($"ETF value '{lineElements[etfIndex].Trim()}' from line '{x}' is unexpected.");
                    }

                    var roundLotSize = int.Parse(lineElements[roundLotSizeIndex].Trim());

                    bool testIssue;
                    if (string.Equals(lineElements[testIssueIndex].Trim(), "N", StringComparison.OrdinalIgnoreCase))
                    {
                        testIssue = false;
                    }
                    else if (string.Equals(lineElements[testIssueIndex].Trim(), "Y", StringComparison.OrdinalIgnoreCase))
                    {
                        testIssue = true;
                    }
                    else
                    {
                        throw new DataProcessFailException($"Test Issue value '{lineElements[testIssueIndex].Trim()}' from line '{x}' is unexpected.");
                    }

                    var nasdaqSymbol = lineElements[nasdaqSymbolIndex].Trim();
                    
                    return new
                    {
                        Ticker = ticker,
                        Name = name,
                        Exchange = exchange,
                        CQSSymbol = cqsSymbol,
                        ETF = etf,
                        RoundLotSize = roundLotSize,
                        TestIssue = testIssue,
                        NASDAQSymbol = nasdaqSymbol,
                        ScrapeTimestampUtc = scrapeTimestampUtc
                    };
                }).ToArray());

            if (!tickerList.Any())
            {
                throw new DataNotScrapedException();
            }

            return JsonConvert.SerializeObject(tickerList);
        }

        /// <summary>
        /// Process Nasdaq listed tickers data and convert to Json
        /// </summary>
        /// <param name="fileContent">File content on Nasdaq FTP for listed tickers</param>
        /// <returns>Nasdaq listed tickers data converted to Json</returns>
        /// <exception cref="CancellationRequestedException">Cancellation is requested.</exception>
        /// <exception cref="InvalidDataException">Data content is invalid.</exception>
        /// <exception cref="DataNotScrapedException">Scraping result is empty.</exception>
        /// <exception cref="DataProcessFailException">The data process operation failed due to unexpected data.</exception>
        private string ProcessAndConvertNasdaqListedToJson(string fileContent)
        {
            const string NasdaqListedFileColumnNamesLine = "Symbol|Security Name|Market Category|Test Issue|Financial Status|Round Lot Size|ETF|NextShares";
            const string TickerColumnName = "Symbol";
            const string NameColumnName = "Security Name";
            const string MarketCategoryColumnName = "Market Category";
            const string TestIssueColumnName = "Test Issue";
            const string FinancialStatusColumnName = "Financial Status";
            const string RoundLotSizeColumnName = "Round Lot Size";
            const string EtfColumnName = "ETF";
            const string NextSharesColumnName = "NextShares";

            var scrapeTimestampUtc = DateTime.UtcNow;

            var lines = fileContent.Split(Environment.NewLine);

            if (!TryFindColumnNamesLineIndex(lines, NasdaqListedFileColumnNamesLine, out var columnNamesLineIndex))
            {
                throw new InvalidDataException("Failed to extract column names line.");
            }

            var columnNames = lines[columnNamesLineIndex].Split("|");
            var tickerIndex = -1;
            var nameIndex = -1;
            var marketCategoryIndex = -1;
            var testIssueIndex = -1;
            var financialStatusIndex = -1;
            var roundLotSizeIndex = -1;
            var etfIndex = -1;
            var nextSharesIndex = -1;

            for (var i = 0; i < columnNames.Length; i++)
            {
                var columnName = columnNames[i];

                switch (columnName)
                {
                    case TickerColumnName:
                        tickerIndex = i;
                        break;
                    case NameColumnName:
                        nameIndex = i;
                        break;
                    case MarketCategoryColumnName:
                        marketCategoryIndex = i;
                        break;
                    case TestIssueColumnName:
                        testIssueIndex = i;
                        break;
                    case FinancialStatusColumnName:
                        financialStatusIndex = i;
                        break;
                    case RoundLotSizeColumnName:
                        roundLotSizeIndex = i;
                        break;
                    case EtfColumnName:
                        etfIndex = i;
                        break;
                    case NextSharesColumnName:
                        nextSharesIndex = i;
                        break;
                    default:
                        break;
                }
            }

            if (tickerIndex == -1 || nameIndex == -1 || marketCategoryIndex == -1 || testIssueIndex == -1 ||
                financialStatusIndex == -1 || roundLotSizeIndex == -1 || etfIndex == -1 || nextSharesIndex == -1)
            {
                throw new InvalidDataException("Failed to extract indexes for required column(s).");
            }

            var tickerList = ScrapeServiceHelper.CreateGenericList(lines.Skip(columnNamesLineIndex + 1)
                .Where(x => !string.IsNullOrEmpty(x) && !string.IsNullOrWhiteSpace(x) && !x.StartsWith("File Creation Time:", StringComparison.OrdinalIgnoreCase))
                .Select(x =>
                {
                    var lineElements = x.Split("|");

                    var ticker = lineElements[tickerIndex].Trim();
                    var name = lineElements[nameIndex].Trim();
                    var marketCategory = lineElements[marketCategoryIndex].Trim();

                    bool testIssue;
                    if (string.Equals(lineElements[testIssueIndex].Trim(), "N", StringComparison.OrdinalIgnoreCase))
                    {
                        testIssue = false;
                    }
                    else if (string.Equals(lineElements[testIssueIndex].Trim(), "Y", StringComparison.OrdinalIgnoreCase))
                    {
                        testIssue = true;
                    }
                    else
                    {
                        throw new DataProcessFailException($"Test Issue value '{lineElements[testIssueIndex].Trim()}' from line '{x}' is unexpected.");
                    }

                    var financialStatus = lineElements[financialStatusIndex].Trim();
                    var roundLotSize = int.Parse(lineElements[roundLotSizeIndex].Trim());

                    bool? etf;
                    if (string.Equals(lineElements[etfIndex].Trim(), "N", StringComparison.OrdinalIgnoreCase))
                    {
                        etf = false;
                    }
                    else if (string.Equals(lineElements[etfIndex].Trim(), "Y", StringComparison.OrdinalIgnoreCase))
                    {
                        etf = true;
                    }
                    else if (string.IsNullOrEmpty(lineElements[etfIndex].Trim()))
                    {
                        etf = null;
                    }
                    else
                    {
                        throw new DataProcessFailException($"ETF value '{lineElements[etfIndex].Trim()}' from line '{x}' is unexpected.");
                    }

                    bool? nextShares;
                    if (string.Equals(lineElements[nextSharesIndex].Trim(), "N", StringComparison.OrdinalIgnoreCase))
                    {
                        nextShares = false;
                    }
                    else if (string.Equals(lineElements[nextSharesIndex].Trim(), "Y", StringComparison.OrdinalIgnoreCase))
                    {
                        nextShares = true;
                    }
                    else if (string.IsNullOrEmpty(lineElements[nextSharesIndex].Trim()))
                    {
                        nextShares = null;
                    }
                    else
                    {
                        throw new DataProcessFailException($"NextShare value '{lineElements[nextSharesIndex].Trim()}' from line '{x}' is unexpected.");
                    }

                    return new
                    {
                        Ticker = ticker,
                        Name = name,
                        MarketCategory = marketCategory,
                        TestIssue = testIssue,
                        FinancialStatus = financialStatus,
                        RoundLotSize = roundLotSize,
                        ETF = etf,
                        NextShares = nextShares,
                        ScrapeTimestampUtc = scrapeTimestampUtc
                    };
                }).ToArray());

            if (!tickerList.Any())
            {
                throw new DataNotScrapedException();
            }

            return JsonConvert.SerializeObject(tickerList);
        }

        private void EnqueueServiceProcedureStatus(string procedureName, Status status, string detail)
        {
            _serviceProcedureStatusQueue.EnqueueServiceProcedureStatus(
                new GrpcServiceProcedureStatus
                {
                    ServiceProcedure = new GrpcServiceProcedure { Service = _serviceName, Procedure = procedureName },
                    Status = status,
                    Detail = detail,
                    UtcTimestamp = DateTime.UtcNow
                });
        }

        private void EnqueueDataToPublish(string procedureName, string data)
        {
            _dataPublishQueue.EnqueueDataToPublish(new DataToPublish
            {
                ServiceProcedure = new GrpcServiceProcedure
                {
                    Service = _serviceName,
                    Procedure = procedureName
                },
                Data = data
            });
        }

        private bool TryFindColumnNamesLineIndex(string[] lines, string columnNamesLine, out int columnNamesLineIndex)
        {
            for (var i = 0; i < lines.Length; i++)
            {

                if (string.Equals(lines[i].Trim(), columnNamesLine,
                    StringComparison.OrdinalIgnoreCase))
                {
                    columnNamesLineIndex = i;
                    return true;
                }
            }

            columnNamesLineIndex = -1;
            return false;
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();

            try
            {
                Task.WaitAll(new[] { _nasdaqListedScrapeTask, _otherListedScrapeTask });
            }
            catch (Exception exception)
            {
                if (exception is AggregateException && exception.InnerException is OperationCanceledException)
                {
                    _logger.LogInformation($"Service '{_serviceName}' is stopped.");
                }
                else
                {
                    _logger.LogError(exception, $"Service '{_serviceName}' is stopped with errors.");
                }
            }

            _cancellationTokenSource.Dispose();
        }
    }
}
