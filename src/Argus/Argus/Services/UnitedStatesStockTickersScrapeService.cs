using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Argus
{
    public class UnitedStatesStockTickersScrapeService : UnitedStatesStockTickersScraper.UnitedStatesStockTickersScraperBase, IDisposable
    {
        private readonly ILogger<UnitedStatesStockTickersScrapeService> _logger;
        private readonly HttpClient _iSharesHttpClient;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly CancellationToken _cancellationToken;
        private readonly DataPublishQueue _dataPublishQueue;
        private readonly GrpcServiceProcedureStatusQueue _serviceProcedureStatusQueue;
        private readonly string _serviceName;

        private HashSet<string> _iSharesCoreSectorsToIgnore;
        private HashSet<string> _iSharesCoreSectorsToInclude;
        private HashSet<string> _iSharesCoreExchangesToIgnore;
        private HashSet<string> _iSharesCoreExchangesToInclude;
        private uint _isScrapingiSharesCoreSPTotal;
        private Task _iSharesCoreSPTotalScrapeTask;
        private uint _isScrapingiSharesCoreSP500;
        private Task _iSharesCoreSP500ScrapeTask;
        private uint _isScrapingiSharesCoreSPMidCap;
        private Task _iSharesCoreSPMidCapScrapeTask;
        private uint _isScrapingiSharesCoreSPSmallCap;
        private Task _iSharesCoreSPSmallCapScrapeTask;

        public UnitedStatesStockTickersScrapeService(ILogger<UnitedStatesStockTickersScrapeService> logger, IHttpClientFactory httpClientFactory, 
            DataPublishQueue dataPublishQueue, GrpcServiceProcedureStatusQueue serviceProcedureStatusQueue, IConfiguration configuration)
        {
            _logger = logger;
            _iSharesHttpClient = httpClientFactory.CreateClient("iShares");
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;
            _dataPublishQueue = dataPublishQueue;
            _serviceProcedureStatusQueue = serviceProcedureStatusQueue;
            _serviceName = GetType().Name;

            _iSharesCoreSectorsToIgnore = new HashSet<string>();
            _iSharesCoreSectorsToInclude = new HashSet<string>();
            _iSharesCoreExchangesToIgnore = new HashSet<string>();
            _iSharesCoreExchangesToInclude = new HashSet<string>();

            configuration.GetSection($"ScrapeServicesConfiguration:{_serviceName}:iSharesCoreSectorsToIgnore").Bind(_iSharesCoreSectorsToIgnore);
            configuration.GetSection($"ScrapeServicesConfiguration:{_serviceName}:iSharesCoreSectorsToInclude").Bind(_iSharesCoreSectorsToInclude);
            configuration.GetSection($"ScrapeServicesConfiguration:{_serviceName}:iSharesCoreExchangesToIgnore").Bind(_iSharesCoreExchangesToIgnore);
            configuration.GetSection($"ScrapeServicesConfiguration:{_serviceName}:iSharesCoreExchangesToInclude").Bind(_iSharesCoreExchangesToInclude);

            _isScrapingiSharesCoreSPTotal = 0;
            _iSharesCoreSPTotalScrapeTask = Task.CompletedTask;

            _isScrapingiSharesCoreSP500 = 0;
            _iSharesCoreSP500ScrapeTask = Task.CompletedTask;

            _isScrapingiSharesCoreSPMidCap = 0;
            _iSharesCoreSPMidCapScrapeTask = Task.CompletedTask;

            _isScrapingiSharesCoreSPSmallCap = 0;
            _iSharesCoreSPSmallCapScrapeTask = Task.CompletedTask;
        }

        public override Task<ScrapeStatusReply> ScrapeiSharesCoreSPTotal(Empty request, ServerCallContext context)
        {
            var procedureName = nameof(ScrapeiSharesCoreSPTotal);

            _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' is called.");

            var isRunning = Interlocked.CompareExchange(ref _isScrapingiSharesCoreSPTotal, 1, 0);
            if (isRunning == 1)
            {
                _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' is already running.");
                return Task.FromResult(new ScrapeStatusReply { Message = "iSharesCoreSPTotal is being scraped" });
            }

            _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' starts to scrape iSharesCoreSPTotal.");
            
            _iSharesCoreSPTotalScrapeTask = ScrapeiSharesCoreSPTotal();
            return Task.FromResult(new ScrapeStatusReply { Message = "starts to scrape iSharesCoreSPTotal" });
        }

        public override Task<ScrapeStatusReply> ScrapeiSharesCoreSP500(Empty request, ServerCallContext context)
        {
            var procedureName = nameof(ScrapeiSharesCoreSP500);

            _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' is called.");

            var isRunning = Interlocked.CompareExchange(ref _isScrapingiSharesCoreSP500, 1, 0);
            if (isRunning == 1)
            {
                _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' is already running.");
                return Task.FromResult(new ScrapeStatusReply { Message = "iSharesCoreSP500 is being scraped" });
            }

            _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' starts to scrape iSharesCoreSP500.");

            _iSharesCoreSP500ScrapeTask = ScrapeiSharesCoreSP500();
            return Task.FromResult(new ScrapeStatusReply { Message = "starts to scrape iSharesCoreSP500" });
        }

        public override Task<ScrapeStatusReply> ScrapeiSharesCoreSPMidCap(Empty request, ServerCallContext context)
        {
            var procedureName = nameof(ScrapeiSharesCoreSPMidCap);

            _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' is called.");

            var isRunning = Interlocked.CompareExchange(ref _isScrapingiSharesCoreSPMidCap, 1, 0);
            if (isRunning == 1)
            {
                _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' is already running.");
                return Task.FromResult(new ScrapeStatusReply { Message = "iSharesCoreSPMidCap is being scraped" });
            }

            _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' starts to scrape iSharesCoreSPMidCap.");

            _iSharesCoreSPMidCapScrapeTask = ScrapeiSharesCoreSPMidCap();
            return Task.FromResult(new ScrapeStatusReply { Message = "starts to scrape iSharesCoreSPMidCap" });
        }

        public override Task<ScrapeStatusReply> ScrapeiSharesCoreSPSmallCap(Empty request, ServerCallContext context)
        {
            var procedureName = nameof(ScrapeiSharesCoreSPSmallCap);

            _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' is called.");

            var isRunning = Interlocked.CompareExchange(ref _isScrapingiSharesCoreSPSmallCap, 1, 0);
            if (isRunning == 1)
            {
                _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' is already running.");
                return Task.FromResult(new ScrapeStatusReply { Message = "iSharesCoreSPSmallCap is being scraped" });
            }

            _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' starts to scrape iSharesCoreSPSmallCap.");

            _iSharesCoreSPSmallCapScrapeTask = ScrapeiSharesCoreSPSmallCap();
            return Task.FromResult(new ScrapeStatusReply { Message = "starts to scrape iSharesCoreSPSmallCap" });
        }

        private async Task ScrapeiSharesCoreSPTotal()
        {
            var procedureName = nameof(ScrapeiSharesCoreSPTotal);
            var requestUri = "us/products/239724/ishares-core-sp-total-us-stock-market-etf/1467271812596.ajax?fileType=csv&fileName=ITOT_holdings&dataType=fund";

            try
            {
                await ScrapeiSharesCoreSPData(procedureName, requestUri);
            }
            finally
            {
                _isScrapingiSharesCoreSPTotal = 0;
            }
        }

        private async Task ScrapeiSharesCoreSP500()
        {
            var procedureName = nameof(ScrapeiSharesCoreSP500);
            var requestUri = "us/products/239726/ishares-core-sp-500-etf/1467271812596.ajax?fileType=csv&fileName=IVV_holdings&dataType=fund";

            try
            {
                await ScrapeiSharesCoreSPData(procedureName, requestUri);
            }
            finally
            {
                _isScrapingiSharesCoreSP500 = 0;
            }
        }

        private async Task ScrapeiSharesCoreSPMidCap()
        {
            var procedureName = nameof(ScrapeiSharesCoreSPMidCap);
            var requestUri = "us/products/239763/ishares-core-sp-midcap-etf/1467271812596.ajax?fileType=csv&fileName=IJH_holdings&dataType=fund";

            try
            {
                await ScrapeiSharesCoreSPData(procedureName, requestUri);
            }
            finally
            {
                _isScrapingiSharesCoreSPMidCap = 0;
            }
        }

        private async Task ScrapeiSharesCoreSPSmallCap()
        {
            var procedureName = nameof(ScrapeiSharesCoreSPSmallCap);
            var requestUri = "us/products/239774/ishares-core-sp-smallcap-etf/1467271812596.ajax?fileType=csv&fileName=IJR_holdings&dataType=fund";

            try
            {
                await ScrapeiSharesCoreSPData(procedureName, requestUri);
            }
            finally
            {
                _isScrapingiSharesCoreSPSmallCap = 0;
            }
        }

        private async Task ScrapeiSharesCoreSPData(string procedureName, string requestUri)
        {
            ScrapeServiceHelper.EnqueueServiceProcedureStatus(_serviceProcedureStatusQueue, _serviceName, procedureName, 
                Status.Information, $"Service '{_serviceName}' procedure '{procedureName}' is scraping data.");

            string csvContent;
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
                {
                    csvContent = await ScrapeServiceHelper.DownloadStringFromWebAsync(_iSharesHttpClient, request, _cancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch (DataNotScrapedException)
            {
                _logger.LogWarning($"Service '{_serviceName}' procedure '{procedureName}' did not scrape any data.");
                ScrapeServiceHelper.EnqueueServiceProcedureStatus(_serviceProcedureStatusQueue, _serviceName, procedureName, Status.Warning, "did not scrape any data");
                
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, $"Service '{_serviceName}' procedure '{procedureName}' failed.");
                ScrapeServiceHelper.EnqueueServiceProcedureStatus(_serviceProcedureStatusQueue, _serviceName, procedureName, Status.Error, $"failed{Environment.NewLine}{exception}");

                return;
            }
            
            try
            {
                var iSharesCoreSPData = ProcessiSharesCoreSPData(csvContent);
                ScrapeServiceHelper.EnqueueDataToPublish(_dataPublishQueue, _serviceName, procedureName, iSharesCoreSPData);
                
                ScrapeServiceHelper.EnqueueServiceProcedureStatus(_serviceProcedureStatusQueue, _serviceName, procedureName, Status.Success, 
                    $"Service '{_serviceName}' procedure '{procedureName}' finished scraping data.");
                _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' finished scraping data.");
            }
            catch (DataNotScrapedException)
            {
                _logger.LogWarning($"Service '{_serviceName}' procedure '{procedureName}' did not scrape any data.");
                ScrapeServiceHelper.EnqueueServiceProcedureStatus(_serviceProcedureStatusQueue, _serviceName, procedureName, Status.Warning, "did not scrape any data");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, $"Service '{_serviceName}' procedure '{procedureName}' failed.");
                ScrapeServiceHelper.EnqueueServiceProcedureStatus(_serviceProcedureStatusQueue, _serviceName, procedureName, Status.Error, $"failed{Environment.NewLine}{exception}");
            }
        }

        /// <summary>
        /// Process iShares core Standard and Poor's tickers data 
        /// </summary>
        /// <param name="rawData">iShares core Standard and Poor's tickers csv file content</param>
        /// <exception cref="DataProcessFailException">The data process operation failed due to unexpected sectors and/or exchanges.</exception>
        /// <exception cref="DataNotScrapedException">Scraping result is empty.</exception>
        /// <exception cref="InvalidDataException">Data content is invalid.</exception>
        /// <returns></returns>
        private string ProcessiSharesCoreSPData(string rawData)
        {
            const string iSharesFileColumnNamesLine =
                "Ticker,Name,Sector,Asset Class,Market Value,Weight (%),Notional Value,Shares,CUSIP,ISIN,SEDOL,Price,Location,Exchange,Currency,FX Rate,Market Currency,Accrual Date";
            const string iSharesFileColumnNamesLineAlternative =
                "Ticker,Name,Type,Sector,Asset Class,Market Value,Weight (%),Notional Value,Shares,CUSIP,ISIN,SEDOL,Price,Location,Exchange,Currency,FX Rate,Market Currency,Accrual Date";
            const string TickerColumnName = "Ticker";
            const string NameColumnName = "Name";
            const string SectorColumnName = "Sector";
            const string AssetClassColumnName = "Asset Class";
            const string CUSIPColumnName = "CUSIP";
            const string ISINColumnName = "ISIN";
            const string SEDOLColumnName = "SEDOL";
            const string ExchangeColumnName = "Exchange";

            var scrapeTimestampUtc = DateTime.UtcNow;

            var lines = rawData.Split(Environment.NewLine);
            if (!ScrapeServiceHelper.TryFindLineIndex(lines, iSharesFileColumnNamesLine, out var columnNamesLineIndex) && 
                !ScrapeServiceHelper.TryFindLineIndex(lines, iSharesFileColumnNamesLineAlternative, out columnNamesLineIndex))
            {
                throw new InvalidDataException("Failed to extract column names line.");
            }

            var unexpectedSectors = new HashSet<string>();
            var unexpectedExchanges = new HashSet<string>();
            var processedTicker = new HashSet<string>();

            var columnNames = lines[columnNamesLineIndex].Split(",");
            var tickerIndex = -1;
            var nameIndex = -1;
            var sectorIndex = -1;
            var assetClassIndex = -1;
            var cusipIndex = -1;
            var isinIndex = -1;
            var sedolIndex = -1;
            var exchangeIndex = -1;
                        
            for (var i = 0; i < columnNames.Length; i++)
            {
                var columnNameTrimmedLowerCase = columnNames[i].Trim().ToLower();

                switch (columnNameTrimmedLowerCase)
                {
                    case TickerColumnName:
                        tickerIndex = i;
                        break;
                    case NameColumnName:
                        nameIndex = i;
                        break;
                    case SectorColumnName:
                        sectorIndex = i;
                        break;
                    case AssetClassColumnName:
                        assetClassIndex = i;
                        break;
                    case CUSIPColumnName:
                        cusipIndex = i;
                        break;
                    case ISINColumnName:
                        isinIndex = i;
                        break;
                    case SEDOLColumnName:
                        sedolIndex = i;
                        break;
                    case ExchangeColumnName:
                        exchangeIndex = i;
                        break;
                    default:
                        break;
                }
            }

            if (tickerIndex == -1 || nameIndex == -1 || sectorIndex == -1 || assetClassIndex == -1 || 
                cusipIndex == -1 || isinIndex == -1 || sedolIndex == -1 || exchangeIndex == -1)
            {
                throw new InvalidDataException("Failed to extract index for required column(s).");
            }

            var stockList = ScrapeServiceHelper.CreateGenericList(new
            {
                Ticker = "",
                Name = "",
                Sector = "",
                CUSIP = "",
                ISIN = "",
                SEDOL = "",
                Exchange = "",
                TimestampUtc = scrapeTimestampUtc
            });

            stockList.Clear();

            for (var i = columnNamesLineIndex + 1; i < lines.Length; i++)
            {
                var continueLoop = false;

                if (string.IsNullOrEmpty(lines[i]) || string.IsNullOrWhiteSpace(lines[i]))
                {
                    break;
                }

                var stockDataElements = lines[i].Split("\",\"");
                if (!string.Equals(stockDataElements[assetClassIndex].Replace("\"", "").Trim(), 
                    "equity", StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                var ticker = stockDataElements[tickerIndex].Replace("\"", "").Trim();
                var sector = stockDataElements[sectorIndex].Replace("\"", "").Trim();
                var exchange = stockDataElements[exchangeIndex].Replace("\"", "").Trim();

                if (ticker == "-" || ticker == "--" || !processedTicker.Add(ticker))
                {
                    continueLoop = true;
                }

                if (!_iSharesCoreSectorsToInclude.Contains(sector))
                {
                    if (!_iSharesCoreSectorsToIgnore.Contains(sector))
                    {
                        unexpectedSectors.Add(sector);
                    }
                    continueLoop = true;
                }

                if (!_iSharesCoreExchangesToInclude.Contains(exchange))
                {
                    if (!_iSharesCoreExchangesToIgnore.Contains(exchange))
                    {
                        unexpectedExchanges.Add(exchange);
                    }
                    continueLoop = true;
                }

                if (continueLoop)
                {
                    continue;
                }

                stockList.Add(new
                {
                    Ticker = stockDataElements[tickerIndex].Replace("\"", "").Trim(),
                    Name = stockDataElements[nameIndex].Replace("\"", "").Trim(),
                    Sector = stockDataElements[sectorIndex].Replace("\"", "").Trim(),
                    CUSIP = stockDataElements[cusipIndex].Replace("\"", "").Trim(),
                    ISIN = stockDataElements[isinIndex].Replace("\"", "").Trim(),
                    SEDOL = stockDataElements[sedolIndex].Replace("\"", "").Trim(),
                    Exchange = stockDataElements[exchangeIndex].Replace("\"", "").Trim(),
                    TimestampUtc = scrapeTimestampUtc
                });
            }

            if (unexpectedSectors.Any() || unexpectedExchanges.Any())
            {
                throw new DataProcessFailException((unexpectedSectors.Any() ? $"Unexpected sectors: {string.Join(",", unexpectedSectors)}{Environment.NewLine}" : "") +
                            (unexpectedExchanges.Any() ? $"Unexpected exchanges: {string.Join(",", unexpectedExchanges)}" : ""));
            }

            if (!stockList.Any())
            {
                throw new DataNotScrapedException();
            }

            return JsonConvert.SerializeObject(stockList);
        }       
                
        public void Dispose()
        {
            _cancellationTokenSource.Cancel();

            try
            {
                Task.WaitAll(new[] { _iSharesCoreSPTotalScrapeTask, _iSharesCoreSP500ScrapeTask, _iSharesCoreSPMidCapScrapeTask, _iSharesCoreSPSmallCapScrapeTask });
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
