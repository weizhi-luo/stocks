using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
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
        private readonly ConcurrentDictionary<string, Exception> _dailyPricesScrapeFailed;

        private uint _isScrapingDailyPricesByTickers;
        private Task _dailyPricesByTickersScrapeTask;
        private readonly ConcurrentDictionary<string, Exception> _dailyPricesByTickersScrapeFailed;

        private const uint UnixTimestampStart = 1262304000;

        public UnitedStatesStockPricesScrapeService(ILogger<UnitedStatesStockPricesScrapeService> logger, IHttpClientFactory httpClientFactory,
                DataPublishQueue dataPublishQueue, GrpcServiceProcedureStatusQueue serviceProcedureStatusQueue, IConfiguration configuration)
        {
            _logger = logger;
            _yahooFinanceHttpClient = httpClientFactory.CreateClient("YahooFinance");
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;
            _dataPublishQueue = dataPublishQueue;
            _serviceProcedureStatusQueue = serviceProcedureStatusQueue;
            _serviceName = GetType().Name;
            _connectionString = configuration.GetConnectionString("Argus");

            _isScrapingDailyPrices = 0;
            _dailyPricesScrapeTask = Task.CompletedTask;
            _dailyPricesScrapeFailed = new ConcurrentDictionary<string, Exception>();

            _isScrapingDailyPricesByTickers = 0;
            _dailyPricesByTickersScrapeTask = Task.CompletedTask;
            _dailyPricesByTickersScrapeFailed = new ConcurrentDictionary<string, Exception>();
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

        private async Task ScrapeYahooFinanceDailyPrices(ConcurrentDictionary<string, Exception> dailyPricesTickersScrapeFailed)
        {
            var procedureName = nameof(ScrapeYahooFinanceDailyPrices);

            try
            {
                var yahooDailyPriceScrapeRecords = DatabaseHelper.Query<YahooDailyPriceScrapeRecord>(_connectionString, 
                    "SELECT Ticker, [Include], BenchmarkDate, BenchmarkOpen, BenchmarkHigh, " + 
                    "BenchmarkLow, BenchmarkClose, BenchmarkAdjustedClose, BenchmarkVolume " +
                    "FROM dbo.YahooDailyPriceScrapeRecord WHERE [Include] = 1", 90);

                await ScrapeYahooFinanceDailyPrices(yahooDailyPriceScrapeRecords, procedureName, dailyPricesTickersScrapeFailed);
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
            finally
            {
                _isScrapingDailyPrices = 0;
            }
        }

        private async Task ScrapeYahooFinanceDailyPricesByTickers(RepeatedField<string> tickers, ConcurrentDictionary<string, Exception> dailyPricesByTickersScrapeFailed)
        {
            var procedureName = nameof(ScrapeYahooFinanceDailyPricesByTickers);

            try
            {
                if (!tickers.Any())
                {
                    _serviceProcedureStatusQueue.EnqueueServiceProcedureStatus(
                        new GrpcServiceProcedureStatus
                        {
                            ServiceProcedure = new GrpcServiceProcedure { Service = _serviceName, Procedure = procedureName },
                            Status = Status.Warning,
                            Detail = "no tickers are provided",
                            UtcTimestamp = DateTime.UtcNow
                        }
                    );
                    _logger.LogWarning($"Service '{_serviceName}' procedure '{procedureName}' did not scrape any data as no tickers are provided.");

                    await Task.CompletedTask;
                    return;
                }

                var yahooDailyPriceScrapeRecords = DatabaseHelper.Query<YahooDailyPriceScrapeRecord>(_connectionString,
                    "SELECT Ticker, [Include], BenchmarkDate, BenchmarkOpen, BenchmarkHigh, " +
                    "BenchmarkLow, BenchmarkClose, BenchmarkAdjustedClose, BenchmarkVolume " +
                    "FROM dbo.YahooDailyPriceScrapeRecord WHERE [Include] = 1 " +
                    $"AND Ticker IN ({string.Join(',', tickers.Select(x => "'" + x + "'"))})", 90);

                if (!yahooDailyPriceScrapeRecords.Any())
                {
                    _serviceProcedureStatusQueue.EnqueueServiceProcedureStatus(
                        new GrpcServiceProcedureStatus
                        {
                            ServiceProcedure = new GrpcServiceProcedure { Service = _serviceName, Procedure = procedureName },
                            Status = Status.Warning,
                            Detail = "no provided tickers can be found from dbo.YahooDailyPriceScrapeRecord",
                            UtcTimestamp = DateTime.UtcNow
                        }
                    );
                    _logger.LogWarning($"Service '{_serviceName}' procedure '{procedureName}' did not scrape any data as no provided tickers can be found from dbo.YahooDailyPriceScrapeRecord.");

                    await Task.CompletedTask;
                    return;
                }

                await ScrapeYahooFinanceDailyPrices(yahooDailyPriceScrapeRecords, procedureName, dailyPricesByTickersScrapeFailed);
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
            finally
            {
                _isScrapingDailyPricesByTickers = 0;
            }
        }

        private async Task ScrapeYahooFinanceDailyPrices(List<YahooDailyPriceScrapeRecord> yahooDailyPriceScrapeRecords, string procedureName,
            ConcurrentDictionary<string, Exception> tickersScrapeFailed)
        {
            for (var i = 0; i < yahooDailyPriceScrapeRecords.Count; i++)
            {
                _serviceProcedureStatusQueue.EnqueueServiceProcedureStatus(
                    new GrpcServiceProcedureStatus
                    {
                        ServiceProcedure = new GrpcServiceProcedure { Service = _serviceName, Procedure = procedureName },
                        Status = Status.Information,
                        Detail = $"Service '{_serviceName}' procedure '{procedureName}' is scraping data.{Environment.NewLine}Process:{i+1}/{yahooDailyPriceScrapeRecords.Count}{Environment.NewLine}Error:{tickersScrapeFailed.Count}",
                        UtcTimestamp = DateTime.UtcNow
                    });

                await Task.Delay(3000);

                if (_cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                Dictionary<DateTime, YahooDailyPrice> tickerDailyPrices;

                var scrapeRecord = yahooDailyPriceScrapeRecords[i];

                try
                {
                    var period1UnixTimestamp = scrapeRecord.BenchmarkDate.HasValue ?
                        ScrapeServiceHelper.GetUnixTimeStamp(scrapeRecord.BenchmarkDate.Value.AddDays(-14)) : UnixTimestampStart;
                    var period2UnixTimestamp = ScrapeServiceHelper.GetUnixTimeStamp(DateTime.Today.AddDays(1));
                    var requestUri = $"{scrapeRecord.Ticker}?period1={period1UnixTimestamp}&period2={period2UnixTimestamp}&interval=1d&events=history&includeAdjustedClose=true";

                    string content = null;
                    using (var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
                    {
                        try
                        {
                            using (var response = await _yahooFinanceHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _cancellationToken))
                            {
                                try
                                {
                                    response.EnsureSuccessStatusCode();
                                }
                                catch (HttpRequestException exception)
                                {
                                    tickersScrapeFailed.AddOrUpdate(scrapeRecord.Ticker, exception, (k, v) => exception);
                                    continue;
                                }

                                content = await response.Content.ReadAsStringAsync(_cancellationToken);
                            }

                            if (string.IsNullOrEmpty(content) || string.IsNullOrWhiteSpace(content))
                            {
                                var exception = new NullReferenceException($"No data can be scraped for ticker '{scrapeRecord.Ticker}'");
                                tickersScrapeFailed.AddOrUpdate(scrapeRecord.Ticker, exception, (k, v) => exception);
                                continue;
                            }
                        }
                        catch (TaskCanceledException exception)
                        {
                            if (_cancellationToken.IsCancellationRequested)
                            {
                                return;
                            }

                            if (exception.InnerException is TimeoutException)
                            {
                                tickersScrapeFailed.AddOrUpdate(scrapeRecord.Ticker, exception.InnerException, (k, v) => exception.InnerException);
                            }
                            else
                            {
                                tickersScrapeFailed.AddOrUpdate(scrapeRecord.Ticker, exception, (k, v) => exception);
                            }

                            continue;
                        }
                        catch (Exception exception)
                        {
                            tickersScrapeFailed.AddOrUpdate(scrapeRecord.Ticker, exception, (k, v) => exception);
                            continue;
                        }
                    }

                    tickerDailyPrices = ProcessYahooDailyPrices(scrapeRecord.Ticker, content, _cancellationToken);
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (scrapeRecord.BenchmarkDate.HasValue && tickerDailyPrices.TryGetValue(scrapeRecord.BenchmarkDate.Value, out var price))
                    {
                        if (price.AdjustedClose != scrapeRecord.BenchmarkAdjustedClose || price.Open != scrapeRecord.BenchmarkOpen || price.High != scrapeRecord.BenchmarkHigh ||
                            price.Low != scrapeRecord.BenchmarkLow || price.Close != scrapeRecord.BenchmarkClose || price.Volume != scrapeRecord.BenchmarkVolume)
                        {
                            yahooDailyPriceScrapeRecords.Add(
                                new YahooDailyPriceScrapeRecord
                                {
                                    Ticker = scrapeRecord.Ticker,
                                    Include = true,
                                    BenchmarkDate = null,
                                    BenchmarkOpen = null,
                                    BenchmarkHigh = null,
                                    BenchmarkLow = null,
                                    BenchmarkClose = null,
                                    BenchmarkAdjustedClose = null,
                                    BenchmarkVolume = null
                                });

                            continue;
                        }
                    }

                    _dataPublishQueue.EnqueueDataToPublish(new DataToPublish
                    {
                        ServiceProcedure = new GrpcServiceProcedure
                        {
                            Service = _serviceName,
                            Procedure = procedureName
                        },
                        Data = JsonConvert.SerializeObject(tickerDailyPrices.Values)
                    });

                    var scrapeRecordToSave = tickerDailyPrices.OrderByDescending(x => x.Key).Skip(1).First().Value;

                    DatabaseHelper.ExecuteStoredProcedure(_connectionString, "dbo.YahooDailyPriceScrapeRecord_InsertUpdateSingleRecord",
                        new
                        {
                            ticker = scrapeRecordToSave.Ticker,
                            include = scrapeRecord.Include,
                            benchmarkDate = scrapeRecordToSave.ValueDate,
                            benchmarkOpen = scrapeRecordToSave.Open,
                            benchmarkHigh = scrapeRecordToSave.High,
                            benchmarkLow = scrapeRecordToSave.Low,
                            benchmarkClose = scrapeRecordToSave.Close,
                            benchmarkAdjustedClose = scrapeRecordToSave.AdjustedClose,
                            benchmarkVolume = scrapeRecordToSave.Volume
                        }, 90);
                }
                catch (Exception exception)
                {
                    tickersScrapeFailed.AddOrUpdate(scrapeRecord.Ticker, exception, (k, v) => exception);
                }
            }

            if (_cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (tickersScrapeFailed.Any())
            {
                var exceptions = new Dictionary<System.Type, HashSet<string>>();

                var enumerator = tickersScrapeFailed.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    _logger.LogError(enumerator.Current.Value, $"Service '{_serviceName}' procedure '{procedureName}' failed to scrape ticker '{enumerator.Current.Key}'");

                    if (exceptions.TryGetValue(enumerator.Current.Value.GetType(), out var tickers))
                    {
                        tickers.Add(enumerator.Current.Key);
                    }
                    else
                    {
                        exceptions.Add(enumerator.Current.Value.GetType(), new HashSet<string> { enumerator.Current.Key });
                    }
                }

                foreach (var pair in exceptions)
                {
                    _serviceProcedureStatusQueue.EnqueueServiceProcedureStatus(
                        new GrpcServiceProcedureStatus
                        {
                            ServiceProcedure = new GrpcServiceProcedure { Service = _serviceName, Procedure = procedureName },
                            Status = Status.Error,
                            Detail = $"failed to scrape tickers:{Environment.NewLine}{string.Join(",", pair.Value)}{Environment.NewLine}With exception type:{pair.Key}",
                            UtcTimestamp = DateTime.UtcNow
                        });
                }
            }
            else
            {
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
        }

        private Dictionary<DateTime, YahooDailyPrice> ProcessYahooDailyPrices(string ticker, string rawData, CancellationToken cancellationToken)
        {
            var scrapeTimestampUtc = DateTime.UtcNow;

            var columnNamesLineIndex = -1;
            var dateIndex = -1;
            var openIndex = -1;
            var highIndex = -1;
            var lowIndex = -1;
            var closeIndex = -1;
            var adjustedCloseIndex = -1;
            var volumeIndex = -1;

            var lines = rawData.Split(Environment.NewLine);

            for (var i = 0; i < lines.Length; i++)
            {
                if (string.Equals(lines[i], "Date,Open,High,Low,Close,Adj Close,Volume", StringComparison.OrdinalIgnoreCase))
                {
                    columnNamesLineIndex = i;
                    break;
                }
            }

            if (columnNamesLineIndex == -1)
            {
                throw new InvalidDataException("Failed to extract column names line.");
            }

            var columnNames = lines[columnNamesLineIndex].Split(",");
            for (var i = 0; i < columnNames.Length; i++)
            {
                var columnNameTrimmedLowerCase = columnNames[i].Trim().ToLower();

                switch (columnNameTrimmedLowerCase)
                {
                    case "date":
                        dateIndex = i;
                        break;
                    case "open":
                        openIndex = i;
                        break;
                    case "high":
                        highIndex = i;
                        break;
                    case "low":
                        lowIndex = i;
                        break;
                    case "close":
                        closeIndex = i;
                        break;
                    case "adj close":
                        adjustedCloseIndex = i;
                        break;
                    case "volume":
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
