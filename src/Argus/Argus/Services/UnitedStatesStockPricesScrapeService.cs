using Google.Protobuf.Collections;
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
    public class UnitedStatesStockPricesScrapeService : UnitedStatesStockPricesScraper.UnitedStatesStockPricesScraperBase, IDisposable
    {
        private readonly ILogger<UnitedStatesStockPricesScrapeService> _logger;
        private readonly HttpClient _yahooFinanceHttpClient;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly CancellationToken _cancellationToken;
        private readonly DataPublishQueue _dataPublishQueue;
        private readonly GrpcServiceProcedureStatusQueue _serviceProcedureStatusQueue;
        private readonly string _serviceName;
        private readonly string _connectionString;
        
        private uint _isScrapingDailyPrices;
        private Task _dailyPricesScrapeTask;
        private readonly Dictionary<string, Exception> _dailyPricesScrapeFailed;

        private uint _isScrapingDailyPricesByTickers;
        private Task _dailyPricesByTickersScrapeTask;
        private readonly Dictionary<string, Exception> _dailyPricesByTickersScrapeFailed;

        private const string YahooFinanceHttpClientName = "YahooFinance";
        private const string DatabaseConnectionStringName = "Argus";
        private const uint FirstJan2010UnixTimestamp = 1262304000;

        public UnitedStatesStockPricesScrapeService(ILogger<UnitedStatesStockPricesScrapeService> logger, IHttpClientFactory httpClientFactory,
                DataPublishQueue dataPublishQueue, GrpcServiceProcedureStatusQueue serviceProcedureStatusQueue, IConfiguration configuration)
        {
            _logger = logger;
            _yahooFinanceHttpClient = httpClientFactory.CreateClient(YahooFinanceHttpClientName);
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;
            _dataPublishQueue = dataPublishQueue;
            _serviceProcedureStatusQueue = serviceProcedureStatusQueue;
            _serviceName = GetType().Name;
            _connectionString = configuration.GetConnectionString(DatabaseConnectionStringName);

            _isScrapingDailyPrices = 0;
            _dailyPricesScrapeTask = Task.CompletedTask;
            _dailyPricesScrapeFailed = new Dictionary<string, Exception>();

            _isScrapingDailyPricesByTickers = 0;
            _dailyPricesByTickersScrapeTask = Task.CompletedTask;
            _dailyPricesByTickersScrapeFailed = new Dictionary<string, Exception>();
        }

        public override Task<ScrapeStatusReply> ScrapeYahooFinanceDailyPrices(Empty request, ServerCallContext context)
        {
            var procedureName = nameof(ScrapeYahooFinanceDailyPrices);

            _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' is called.");

            var isRunning = Interlocked.CompareExchange(ref _isScrapingDailyPrices, 1, 0);
            if (isRunning == 1)
            {
                _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' is already running.");
                return Task.FromResult(new ScrapeStatusReply { Message = "Daily prices are being scraped" });
            }

            _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' starts to scrape Yahoo finance daily prices.");

            _dailyPricesScrapeFailed.Clear();

            _dailyPricesScrapeTask = ScrapeYahooFinanceDailyPrices(_dailyPricesScrapeFailed);
            return Task.FromResult(new ScrapeStatusReply { Message = "starts to scrape Yahoo finance daily prices" });
        }

        public override Task<ScrapeStatusReply> ScrapeYahooFinanceDailyPricesByTickers(ScrapeWithTickersRequest request, ServerCallContext context)
        {
            var procedureName = nameof(ScrapeYahooFinanceDailyPricesByTickers);

            _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' is called.");

            var isRunning = Interlocked.CompareExchange(ref _isScrapingDailyPricesByTickers, 1, 0);
            if (isRunning == 1)
            {
                _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' is already running.");
                return Task.FromResult(new ScrapeStatusReply { Message = "Daily prices are being scraped" });
            }

            _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' starts to scrape Yahoo finance daily prices.");

            _dailyPricesByTickersScrapeFailed.Clear();

            _dailyPricesByTickersScrapeTask = ScrapeYahooFinanceDailyPricesByTickers(request.Tickers, _dailyPricesByTickersScrapeFailed);
            return Task.FromResult(new ScrapeStatusReply { Message = "starts to scrape Yahoo finance daily prices by tickers" });
        }

        private async Task ScrapeYahooFinanceDailyPrices(Dictionary<string, Exception> dailyPricesTickersScrapeFailed)
        {
            const string DatabaseQuery = @"SELECT Ticker, [Include], BenchmarkDate, BenchmarkOpen, BenchmarkHigh, 
                                           BenchmarkLow, BenchmarkClose, BenchmarkAdjustedClose, BenchmarkVolume 
                                           FROM dbo.YahooDailyPriceScrapeRecord WHERE [Include] = 1";

            var procedureName = nameof(ScrapeYahooFinanceDailyPrices);

            try
            {
                var yahooDailyPriceScrapeRecords = DatabaseHelper.Query<YahooDailyPriceScrapeRecord>(_connectionString, DatabaseQuery, 90);

                await ScrapeYahooFinanceDailyPrices(yahooDailyPriceScrapeRecords, procedureName, dailyPricesTickersScrapeFailed);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, $"Service '{_serviceName}' procedure '{procedureName}' failed.");
                ScrapeServiceHelper.EnqueueServiceProcedureStatus(_serviceProcedureStatusQueue, _serviceName, 
                    procedureName, Status.Error, $"failed{Environment.NewLine}{exception}");
            }
            finally
            {
                _isScrapingDailyPrices = 0;
            }
        }

        private async Task ScrapeYahooFinanceDailyPricesByTickers(RepeatedField<string> tickers, Dictionary<string, Exception> dailyPricesByTickersScrapeFailed)
        {
            var procedureName = nameof(ScrapeYahooFinanceDailyPricesByTickers);

            try
            {
                if (!tickers.Any())
                {
                    ScrapeServiceHelper.EnqueueServiceProcedureStatus(_serviceProcedureStatusQueue, _serviceName, 
                        procedureName, Status.Warning, "no tickers are provided");
                    _logger.LogWarning($"Service '{_serviceName}' procedure '{procedureName}' did not scrape any data as no tickers are provided.");

                    await Task.CompletedTask;
                    return;
                }

                var yahooDailyPriceScrapeRecords = DatabaseHelper.Query<YahooDailyPriceScrapeRecord>(_connectionString,
                    @$"SELECT Ticker, [Include], BenchmarkDate, BenchmarkOpen, BenchmarkHigh, 
                       BenchmarkLow, BenchmarkClose, BenchmarkAdjustedClose, BenchmarkVolume 
                       FROM dbo.YahooDailyPriceScrapeRecord WHERE [Include] = 1 
                       AND Ticker IN ({string.Join(',', tickers.Select(x => "'" + x + "'"))})", 90);

                if (!yahooDailyPriceScrapeRecords.Any())
                {
                    ScrapeServiceHelper.EnqueueServiceProcedureStatus(_serviceProcedureStatusQueue, _serviceName, 
                        procedureName, Status.Warning, "provided ticker(s) cannot be found from dbo.YahooDailyPriceScrapeRecord");
                    _logger.LogWarning(
                        $"Service '{_serviceName}' procedure '{procedureName}' did not scrape any data as provided ticker(s) cannot be found from dbo.YahooDailyPriceScrapeRecord.");

                    await Task.CompletedTask;
                    return;
                }

                await ScrapeYahooFinanceDailyPrices(yahooDailyPriceScrapeRecords, procedureName, dailyPricesByTickersScrapeFailed);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, $"Service '{_serviceName}' procedure '{procedureName}' failed.");
                ScrapeServiceHelper.EnqueueServiceProcedureStatus(_serviceProcedureStatusQueue, _serviceName, 
                    procedureName, Status.Error, $"failed{Environment.NewLine}{exception}");
            }
            finally
            {
                _isScrapingDailyPricesByTickers = 0;
            }
        }

        private async Task ScrapeYahooFinanceDailyPrices(List<YahooDailyPriceScrapeRecord> yahooDailyPriceScrapeRecords, string procedureName,
            Dictionary<string, Exception> tickersScrapeFailed)
        {
            for (var i = 0; i < yahooDailyPriceScrapeRecords.Count; i++)
            {
                ScrapeServiceHelper.EnqueueServiceProcedureStatus(_serviceProcedureStatusQueue, _serviceName, procedureName, Status.Information, 
                    $"Service '{_serviceName}' procedure '{procedureName}' is scraping data.{Environment.NewLine}Process:{i+1}/{yahooDailyPriceScrapeRecords.Count}{Environment.NewLine}Error:{tickersScrapeFailed.Count}");

                try
                {
                    await Task.Delay(3000, _cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                
                var scrapeRecord = yahooDailyPriceScrapeRecords[i];

                string fileContent;
                var requestUri = CreateRequestUri(scrapeRecord);
                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
                    {
                        fileContent = await ScrapeServiceHelper.DownloadStringFromWebAsync(_yahooFinanceHttpClient, request, _cancellationToken);
                    }
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                catch (Exception exception)
                {
                    if (exception is DataNotScrapedException)
                    {
                        _logger.LogWarning(exception, $"Service '{_serviceName}' procedure '{procedureName}' does not scrape any data for ticker '{scrapeRecord.Ticker}'");
                    }
                    else
                    {
                        _logger.LogError(exception, $"Service '{_serviceName}' procedure '{procedureName}' failed to scrape ticker '{scrapeRecord.Ticker}'");
                    }
                    AddOrUpdateTickerScrapeFailures(tickersScrapeFailed, scrapeRecord.Ticker, exception);
                    
                    continue;
                }

                Dictionary<DateTime, YahooDailyPrice> tickerDailyPrices;
                try
                {
                    tickerDailyPrices = ProcessYahooDailyPrices(scrapeRecord.Ticker, fileContent);
                }
                catch (InvalidDataException exception)
                {
                    _logger.LogError(exception, $"Service '{_serviceName}' procedure '{procedureName}' failed to process data for ticker '{scrapeRecord.Ticker}'");
                    AddOrUpdateTickerScrapeFailures(tickersScrapeFailed, scrapeRecord.Ticker, exception);
                    
                    continue;
                }

                bool rescrapeNeeded;
                try
                {
                    rescrapeNeeded = CheckIfRescrapeNeeded(scrapeRecord, tickerDailyPrices);
                }
                catch (DataScrapeFailException exception)
                {
                    _logger.LogError(exception, $"Service '{_serviceName}' procedure '{procedureName}' failed to scrape ticker '{scrapeRecord.Ticker}'");
                    AddOrUpdateTickerScrapeFailures(tickersScrapeFailed, scrapeRecord.Ticker, exception);
                    
                    continue;
                }

                if (rescrapeNeeded)
                {
                    InstructToRescrapeTicker(yahooDailyPriceScrapeRecords, scrapeRecord.Ticker);
                    continue;
                }

                ScrapeServiceHelper.EnqueueDataToPublish(_dataPublishQueue, _serviceName, procedureName, JsonConvert.SerializeObject(tickerDailyPrices.Values));

                var scrapeRecordToSave = tickerDailyPrices.OrderByDescending(x => x.Key).Skip(1).First().Value;
                InsertUpdateScrapeRecord(scrapeRecordToSave.Ticker, scrapeRecord.Include, scrapeRecordToSave.ValueDate, scrapeRecordToSave.Open,
                    scrapeRecordToSave.High, scrapeRecordToSave.Low, scrapeRecordToSave.Close, scrapeRecordToSave.AdjustedClose, scrapeRecordToSave.Volume);
            }

            if (_cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (tickersScrapeFailed.Any())
            {
                var exceptions = ProcessScrapeFailures(tickersScrapeFailed);

                foreach (var pair in exceptions)
                {
                    if (pair.Key == typeof(DataNotScrapedException))
                    {
                        ScrapeServiceHelper.EnqueueServiceProcedureStatus(_serviceProcedureStatusQueue, _serviceName, procedureName, Status.Warning, 
                            $"no data can be scraped for tickers:{Environment.NewLine}{string.Join(",", pair.Value)}");
                        continue;
                    }

                    ScrapeServiceHelper.EnqueueServiceProcedureStatus(_serviceProcedureStatusQueue, _serviceName, procedureName, Status.Error, 
                        $"failed to scrape tickers:{Environment.NewLine}{string.Join(",", pair.Value)}{Environment.NewLine}With exception type:{pair.Key}");
                }
            }
            else
            {
                _logger.LogInformation($"Service '{_serviceName}' procedure '{procedureName}' finished scraping data.");
                ScrapeServiceHelper.EnqueueServiceProcedureStatus(_serviceProcedureStatusQueue, _serviceName, procedureName, Status.Success,
                        $"Service '{_serviceName}' procedure '{procedureName}' finished scraping data.");
            }
        }

        private bool CheckIfRescrapeNeeded(YahooDailyPriceScrapeRecord scrapeRecord, Dictionary<DateTime, YahooDailyPrice> tickerDailyPrices)
        {
            if (!scrapeRecord.BenchmarkDate.HasValue)
            {
                return false;
            }

            if (!tickerDailyPrices.TryGetValue(scrapeRecord.BenchmarkDate.Value, out var price))
            {
                throw new DataScrapeFailException($"Failed to get scraped data for date {scrapeRecord.BenchmarkDate.Value:yyyy-MM-dd} to check if rescrape is needed.");
            }

            return price.AdjustedClose != scrapeRecord.BenchmarkAdjustedClose ||
                price.Open != scrapeRecord.BenchmarkOpen || 
                price.High != scrapeRecord.BenchmarkHigh ||
                price.Low != scrapeRecord.BenchmarkLow || 
                price.Close != scrapeRecord.BenchmarkClose || 
                price.Volume != scrapeRecord.BenchmarkVolume;
        }

        private void AddOrUpdateTickerScrapeFailures(Dictionary<string, Exception> tickersScrapeFailures, string ticker, Exception exception)
        {
            if (tickersScrapeFailures.ContainsKey(ticker))
            {
                tickersScrapeFailures[ticker] = exception;
            }
            else
            {
                tickersScrapeFailures.Add(ticker, exception);
            }
        }

        private Dictionary<System.Type, HashSet<string>> ProcessScrapeFailures(Dictionary<string, Exception> tickersScrapeFailed)
        {
            var exceptions = new Dictionary<System.Type, HashSet<string>>();

            var enumerator = tickersScrapeFailed.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (exceptions.TryGetValue(enumerator.Current.Value.GetType(), out var tickers))
                {
                    tickers.Add(enumerator.Current.Key);
                }
                else
                {
                    exceptions.Add(enumerator.Current.Value.GetType(), new HashSet<string> { enumerator.Current.Key });
                }
            }

            return exceptions;
        }

        private Dictionary<DateTime, YahooDailyPrice> ProcessYahooDailyPrices(string ticker, string rawData)
        {
            const string YahooDailyPricesFileColumnNamesLine = "Date,Open,High,Low,Close,Adj Close,Volume";
            const string DateColumnName = "Date";
            const string OpenColumnName = "Open";
            const string HighColumnName = "High";
            const string LowColumnName = "Low";
            const string CloseColumnName = "Close";
            const string AdjustedCloseColumnName = "Adj Close";
            const string VolumeColumnName = "Volume";

            var scrapeTimestampUtc = DateTime.UtcNow;

            var lines = rawData.Split(Environment.NewLine);

            if (!ScrapeServiceHelper.TryFindLineIndex(lines, YahooDailyPricesFileColumnNamesLine, out var columnNamesLineIndex))
            {
                throw new InvalidDataException("Failed to extract column names line.");
            }

            var columnNames = lines[columnNamesLineIndex].Split(",");
            var dateIndex = -1;
            var openIndex = -1;
            var highIndex = -1;
            var lowIndex = -1;
            var closeIndex = -1;
            var adjustedCloseIndex = -1;
            var volumeIndex = -1;

            for (var i = 0; i < columnNames.Length; i++)
            {
                var columnNameTrimmedLowerCase = columnNames[i].Trim().ToLower();

                switch (columnNameTrimmedLowerCase)
                {
                    case DateColumnName:
                        dateIndex = i;
                        break;
                    case OpenColumnName:
                        openIndex = i;
                        break;
                    case HighColumnName:
                        highIndex = i;
                        break;
                    case LowColumnName:
                        lowIndex = i;
                        break;
                    case CloseColumnName:
                        closeIndex = i;
                        break;
                    case AdjustedCloseColumnName:
                        adjustedCloseIndex = i;
                        break;
                    case VolumeColumnName:
                        volumeIndex = i;
                        break;
                    default:
                        break;
                }
            }

            if (dateIndex == -1 || openIndex == -1 || highIndex == -1 || lowIndex == -1 || 
                closeIndex == -1 || adjustedCloseIndex == -1 || volumeIndex == -1)
            {
                throw new InvalidDataException("Failed to extract index for required column(s).");
            }

            var result = new Dictionary<DateTime, YahooDailyPrice>();

            foreach (var line in lines.Skip(columnNamesLineIndex + 1))
            {
                if (string.IsNullOrEmpty(line) || string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var lineElements = line.Split(",");

                var valueDate = DateTime.ParseExact(lineElements[dateIndex].Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                var open = string.IsNullOrEmpty(lineElements[openIndex]) || string.IsNullOrWhiteSpace(lineElements[openIndex]) || 
                    string.Equals(lineElements[openIndex], "null", StringComparison.OrdinalIgnoreCase) ? (decimal?)null : decimal.Parse(lineElements[openIndex]);
                var high = string.IsNullOrEmpty(lineElements[highIndex]) || string.IsNullOrWhiteSpace(lineElements[highIndex]) ||
                    string.Equals(lineElements[highIndex], "null", StringComparison.OrdinalIgnoreCase) ? (decimal?)null : decimal.Parse(lineElements[highIndex]);
                var low = string.IsNullOrEmpty(lineElements[lowIndex]) || string.IsNullOrWhiteSpace(lineElements[lowIndex]) ||
                    string.Equals(lineElements[lowIndex], "null", StringComparison.OrdinalIgnoreCase) ? (decimal?)null : decimal.Parse(lineElements[lowIndex]);
                var close = string.IsNullOrEmpty(lineElements[closeIndex]) || string.IsNullOrWhiteSpace(lineElements[closeIndex]) || 
                    string.Equals(lineElements[closeIndex], "null", StringComparison.OrdinalIgnoreCase) ? (decimal?)null : decimal.Parse(lineElements[closeIndex]);
                var adjustedClose = string.IsNullOrEmpty(lineElements[adjustedCloseIndex]) || string.IsNullOrWhiteSpace(lineElements[adjustedCloseIndex]) || 
                    string.Equals(lineElements[adjustedCloseIndex], "null", StringComparison.OrdinalIgnoreCase) ? (decimal?)null : decimal.Parse(lineElements[adjustedCloseIndex]);
                var volume = string.IsNullOrEmpty(lineElements[volumeIndex]) || string.IsNullOrWhiteSpace(lineElements[volumeIndex]) ||
                    string.Equals(lineElements[volumeIndex], "null", StringComparison.OrdinalIgnoreCase) ? (decimal?)null : decimal.Parse(lineElements[volumeIndex]);

                result.Add(valueDate, new YahooDailyPrice
                {
                    Ticker = ticker,
                    ValueDate = valueDate,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    AdjustedClose = adjustedClose,
                    Volume = volume,
                    ScrapeTimestampUtc = scrapeTimestampUtc
                });
            }

            return result;
        }

        private void InsertUpdateScrapeRecord(string ticker, bool include, DateTime valueDate, decimal? open,
            decimal? high, decimal? low, decimal? close, decimal? adjustedClose, decimal? volume)
        {
            DatabaseHelper.ExecuteStoredProcedure(_connectionString, "dbo.YahooDailyPriceScrapeRecord_InsertUpdateSingleRecord",
                new
                {
                    ticker = ticker,
                    include = include,
                    benchmarkDate = valueDate,
                    benchmarkOpen = open,
                    benchmarkHigh = high,
                    benchmarkLow = low,
                    benchmarkClose = close,
                    benchmarkAdjustedClose = adjustedClose,
                    benchmarkVolume = volume
                }, 90);
        }

        private string CreateRequestUri(YahooDailyPriceScrapeRecord scrapeRecord)
        {
            var periodStartUnixTimestamp = scrapeRecord.BenchmarkDate.HasValue ?
                        ScrapeServiceHelper.GetUnixTimeStamp(scrapeRecord.BenchmarkDate.Value.AddDays(-14)) : FirstJan2010UnixTimestamp;
            var periodEndUnixTimestamp = ScrapeServiceHelper.GetUnixTimeStamp(DateTime.Today.AddDays(1));
            
            return $"{scrapeRecord.Ticker}?period1={periodStartUnixTimestamp}&period2={periodEndUnixTimestamp}&interval=1d&events=history&includeAdjustedClose=true";
        }

        private void InstructToRescrapeTicker(List<YahooDailyPriceScrapeRecord> yahooDailyPriceScrapeRecords, string ticker)
        {
            yahooDailyPriceScrapeRecords.Add(
                new YahooDailyPriceScrapeRecord
                {
                    Ticker = ticker,
                    Include = true,
                    BenchmarkDate = null,
                    BenchmarkOpen = null,
                    BenchmarkHigh = null,
                    BenchmarkLow = null,
                    BenchmarkClose = null,
                    BenchmarkAdjustedClose = null,
                    BenchmarkVolume = null
                });
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();

            try
            {
                Task.WaitAll(new[] { _dailyPricesScrapeTask, _dailyPricesByTickersScrapeTask });
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

        private class YahooDailyPriceScrapeRecord
        {
            public string Ticker { get; set; }
            public bool Include { get; set; }
            public DateTime? BenchmarkDate { get; set; }
            public decimal? BenchmarkOpen { get; set; }
            public decimal? BenchmarkHigh { get; set; }
            public decimal? BenchmarkLow { get; set; }
            public decimal? BenchmarkClose { get; set; }
            public decimal? BenchmarkAdjustedClose { get; set; }
            public decimal? BenchmarkVolume { get; set; }
        }

        private class YahooDailyPrice
        {
            public string Ticker { get; set; }
            public DateTime ValueDate { get; set; }
            public decimal? Open { get; set; }
            public decimal? High { get; set; }
            public decimal? Low { get; set; }
            public decimal? Close { get; set; }
            public decimal? AdjustedClose { get; set; }
            public decimal? Volume { get; set; }
            public DateTime ScrapeTimestampUtc { get; set; }
        }
    }

    
}
