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
        private readonly ILogger<NasdaqTickersScrapeService> _logger;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly CancellationToken _cancellationToken;
        private readonly DataPublishQueue _dataPublishQueue;
        private readonly GrpcServiceProcedureStatusQueue _serviceProcedureStatusQueue;
        private readonly string _serviceName;

        private uint _isScrapingNasdaqListed;
        private Task _nasdaqListedScrapeTask;
        private uint _isScrapingOtherListed;
        private Task _otherListedScrapeTask;

        public NasdaqTickersScrapeService(ILogger<NasdaqTickersScrapeService> logger, DataPublishQueue dataPublishQueue, GrpcServiceProcedureStatusQueue serviceProcedureStatusQueue)
        {
            _logger = logger;
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;
            _dataPublishQueue = dataPublishQueue;
            _serviceProcedureStatusQueue = serviceProcedureStatusQueue;
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
            var procedureName = nameof(ScrapeNasdaqListed);
            var ftpFilePath = "ftp://ftp.nasdaqtrader.com/symboldirectory/nasdaqlisted.txt";

            try
            {
                await ScrapeNasdaqData(procedureName, ftpFilePath, ProcessAndConvertNasdaqListedToJson);
            }
            finally
            {
                _isScrapingNasdaqListed = 0;
            }
        }

        private async Task ScrapeNasdaqOtherListed()
        {
            var procedureName = nameof(ScrapeNasdaqOtherListed);
            var ftpFilePath = "ftp://ftp.nasdaqtrader.com/symboldirectory/otherlisted.txt";

            try
            {
                await ScrapeNasdaqData(procedureName, ftpFilePath, ProcessAndConvertNasdaqOtherListedToJson);
            }
            finally
            {
                _isScrapingOtherListed = 0;
            }
        }

        private async Task ScrapeNasdaqData(string procedureName, string ftpFilePath, Func<string, CancellationToken, string> ProcessAndConvertNasdaqDataToJson)
        {
            _serviceProcedureStatusQueue.EnqueueServiceProcedureStatus(
                new GrpcServiceProcedureStatus
                {
                    ServiceProcedure = new GrpcServiceProcedure { Service = _serviceName, Procedure = procedureName },
                    Status = Status.Information,
                    Detail = $"Service '{_serviceName}' procedure '{procedureName}' is scraping data.",
                    UtcTimestamp = DateTime.UtcNow
                });

            string fileContent;

            try
            {
                fileContent = await ScrapeServiceHelper.DownloadFtpFileContentAsync(ftpFilePath, "anonymous", "");
            }
            catch (DataScrapeFailException exception)
            {
                _logger.LogError(exception, $"Service '{_serviceName}' procedure '{procedureName}' failed.");
                _serviceProcedureStatusQueue.EnqueueServiceProcedureStatus(
                    new GrpcServiceProcedureStatus
                    {
                        ServiceProcedure = new GrpcServiceProcedure { Service = _serviceName, Procedure = procedureName },
                        Status = Status.Error,
                        Detail = $"failed{Environment.NewLine}{exception}",
                        UtcTimestamp = DateTime.UtcNow
                    });
                
                return;
            }
            catch (DataNotScrapedException)
            {
                _logger.LogWarning($"Service '{_serviceName}' procedure '{procedureName}' did not scrape any data.");
                _serviceProcedureStatusQueue.EnqueueServiceProcedureStatus(
                    new GrpcServiceProcedureStatus
                    {
                        ServiceProcedure = new GrpcServiceProcedure { Service = _serviceName, Procedure = procedureName },
                        Status = Status.Warning,
                        Detail = "did not scrape any data",
                        UtcTimestamp = DateTime.UtcNow
                    });

                return;
            }

            try
            {
                string nasdaqData;

                try
                {
                    nasdaqData = ProcessAndConvertNasdaqDataToJson(fileContent, _cancellationToken);
                }
                catch (CancellationRequestedException)
                {
                    return;
                }
                catch (DataNotScrapedException)
                {
                    _logger.LogWarning($"Service '{_serviceName}' procedure '{procedureName}' did not scrape any data.");
                    _serviceProcedureStatusQueue.EnqueueServiceProcedureStatus(
                        new GrpcServiceProcedureStatus
                        {
                            ServiceProcedure = new GrpcServiceProcedure { Service = _serviceName, Procedure = procedureName },
                            Status = Status.Warning,
                            Detail = "did not scrape any data",
                            UtcTimestamp = DateTime.UtcNow
                        }
                    );
                    
                    return;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, $"Service '{_serviceName}' procedure '{procedureName}' failed.");
                    _serviceProcedureStatusQueue.EnqueueServiceProcedureStatus(
                        new GrpcServiceProcedureStatus
                        {
                            ServiceProcedure = new GrpcServiceProcedure { Service = _serviceName, Procedure = procedureName },
                            Status = Status.Error,
                            Detail = $"failed{Environment.NewLine}{exception}",
                            UtcTimestamp = DateTime.UtcNow
                        });

                    return;
                }
                
                if (_cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                _dataPublishQueue.EnqueueDataToPublish(new DataToPublish
                {
                    ServiceProcedure = new GrpcServiceProcedure
                    {
                        Service = _serviceName,
                        Procedure = procedureName
                    },
                    Data = nasdaqData
                });

                _serviceProcedureStatusQueue.EnqueueServiceProcedureStatus(
                    new GrpcServiceProcedureStatus
                    {
                        ServiceProcedure = new GrpcServiceProcedure { Service = _serviceName, Procedure = procedureName },
                        Status = Status.Success,
                        Detail = $"Service '{_serviceName}' procedure '{procedureName}' finished scraping data.",
                        UtcTimestamp = DateTime.UtcNow
                    });

                _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' finished scraping data.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, $"Service '{_serviceName}' procedure '{procedureName}' failed.");
                _serviceProcedureStatusQueue.EnqueueServiceProcedureStatus(
                    new GrpcServiceProcedureStatus
                    {
                        ServiceProcedure = new GrpcServiceProcedure { Service = _serviceName, Procedure = procedureName },
                        Status = Status.Error,
                        Detail = $"failed{Environment.NewLine}{exception}",
                        UtcTimestamp = DateTime.UtcNow
                    });
            }
        }

        /// <summary>
        /// Process other listed tickers data and convert to Json
        /// </summary>
        /// <param name="fileContent">File content on Nasdaq FTP for other listed tickers</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Other listed tickers data converted to Json</returns>
        /// <exception cref="CancellationRequestedException">Cancellation is requested.</exception>
        /// <exception cref="InvalidDataException">Data content is invalid.</exception>
        /// <exception cref="DataNotScrapedException">Scraping result is empty.</exception>
        /// <exception cref="DataProcessFailException">The data process operation failed due to unexpected data.</exception>
        private string ProcessAndConvertNasdaqOtherListedToJson(string fileContent, CancellationToken cancellationToken)
        {
            var scrapeTimestampUtc = DateTime.UtcNow;

            var columnNamesLineIndex = -1;
            var tickerIndex = -1;
            var nameIndex = -1;
            var exchangeIndex = -1;
            var cqsSymbolIndex = -1;
            var etfIndex = -1;
            var roundLotSizeIndex = -1;
            var testIssueIndex = -1;
            var nasdaqSymbolIndex = -1;

            var lines = fileContent.Split(Environment.NewLine);

            for (var i = 0; i < lines.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new CancellationRequestedException();
                }

                if (string.Equals(lines[i].Trim(), "ACT Symbol|Security Name|Exchange|CQS Symbol|ETF|Round Lot Size|Test Issue|NASDAQ Symbol",
                    StringComparison.InvariantCultureIgnoreCase))
                {
                    columnNamesLineIndex = i;
                    break;
                }
            }

            if (columnNamesLineIndex == -1)
            {
                throw new InvalidDataException("Failed to extract column names line.");
            }

            var columnNames = lines[columnNamesLineIndex].Split("|");
            for (var i = 0; i < columnNames.Length; i++)
            {
                var columnNameTrimmedLowerCase = columnNames[i].Trim().ToLower();

                switch (columnNameTrimmedLowerCase)
                {
                    case "act symbol":
                        tickerIndex = i;
                        break;
                    case "security name":
                        nameIndex = i;
                        break;
                    case "exchange":
                        exchangeIndex = i;
                        break;
                    case "cqs symbol":
                        cqsSymbolIndex = i;
                        break;
                    case "etf":
                        etfIndex = i;
                        break;
                    case "round lot size":
                        roundLotSizeIndex = i;
                        break;
                    case "test issue":
                        testIssueIndex = i;
                        break;
                    case "nasdaq symbol":
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
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Nasdaq listed tickers data converted to Json</returns>
        /// <exception cref="CancellationRequestedException">Cancellation is requested.</exception>
        /// <exception cref="InvalidDataException">Data content is invalid.</exception>
        /// <exception cref="DataNotScrapedException">Scraping result is empty.</exception>
        /// <exception cref="DataProcessFailException">The data process operation failed due to unexpected data.</exception>
        private string ProcessAndConvertNasdaqListedToJson(string fileContent, CancellationToken cancellationToken)
        {
            var scrapeTimestampUtc = DateTime.UtcNow;

            var columnNamesLineIndex = -1;
            var tickerIndex = -1;
            var nameIndex = -1;
            var marketCategoryIndex = -1;
            var testIssueIndex = -1;
            var financialStatusIndex = -1;
            var roundLotSizeIndex = -1;
            var etfIndex = -1;
            var nextSharesIndex = -1;

            var lines = fileContent.Split(Environment.NewLine);

            for (var i = 0; i < lines.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new CancellationRequestedException();
                }

                if (string.Equals(lines[i].Trim(), "Symbol|Security Name|Market Category|Test Issue|Financial Status|Round Lot Size|ETF|NextShares", 
                    StringComparison.InvariantCultureIgnoreCase))
                {
                    columnNamesLineIndex = i;
                    break;
                }
            }

            if (columnNamesLineIndex == -1)
            {
                throw new InvalidDataException("Failed to extract column names line.");
            }

            var columnNames = lines[columnNamesLineIndex].Split("|");
            for (var i = 0; i < columnNames.Length; i++)
            {
                var columnNameTrimmedLowerCase = columnNames[i].Trim().ToLower();

                switch (columnNameTrimmedLowerCase)
                {
                    case "symbol":
                        tickerIndex = i;
                        break;
                    case "security name":
                        nameIndex = i;
                        break;
                    case "market category":
                        marketCategoryIndex = i;
                        break;
                    case "test issue":
                        testIssueIndex = i;
                        break;
                    case "financial status":
                        financialStatusIndex = i;
                        break;
                    case "round lot size":
                        roundLotSizeIndex = i;
                        break;
                    case "etf":
                        etfIndex = i;
                        break;
                    case "nextshares":
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
