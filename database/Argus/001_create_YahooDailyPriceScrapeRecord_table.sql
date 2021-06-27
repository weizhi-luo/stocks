USE Argus
GO

CREATE TABLE dbo.YahooDailyPriceScrapeRecord (
    [Ticker] VARCHAR(50) NOT NULL,
	[Include] BIT NOT NULL,
	[BenchmarkDate] DATETIME NULL,
	[BenchmarkOpen] DECIMAL(18,6) NULL,
	[BenchmarkHigh] DECIMAL(18,6) NULL,
	[BenchmarkLow] DECIMAL(18,6) NULL,
	[BenchmarkClose] DECIMAL(18,6) NULL,
	[BenchmarkAdjustedClose] DECIMAL(18,6) NULL,
	[BenchmarkVolume] DECIMAL(18,6) NULL
)

GO

CREATE NONCLUSTERED INDEX IX_YahooDailyPriceScrapeRecord_Include ON dbo.YahooDailyPriceScrapeRecord (
    [Include]
)
INCLUDE (
    [Ticker],
	[BenchmarkDate],
	[BenchmarkOpen],
	[BenchmarkHigh],
	[BenchmarkLow],
	[BenchmarkClose],
	[BenchmarkAdjustedClose],
	[BenchmarkVolume]
)

GO

CREATE NONCLUSTERED INDEX IX_YahooDailyPriceScrapeRecord_Ticker ON dbo.YahooDailyPriceScrapeRecord (
    [Ticker]
)
INCLUDE (
    [Include],
	[BenchmarkDate],
	[BenchmarkOpen],
	[BenchmarkHigh],
	[BenchmarkLow],
	[BenchmarkClose],
	[BenchmarkAdjustedClose],
	[BenchmarkVolume]
)

GO