USE Metis
GO

CREATE TYPE dbo.UnitedStatesStockDailyPriceDataUtc AS TABLE
(
    [Ticker] [VARCHAR](50) NOT NULL,
	[Date] [DATE] NOT NULL,
	[Open] [DECIMAL](18,6) NOT NULL,
	[High] [DECIMAL](18,6) NOT NULL,
	[Low] [DECIMAL](18,6) NOT NULL,
	[Close] [DECIMAL](18,6) NOT NULL,
	[AdjustedClose] [DECIMAL](18,6) NOT NULL,
	[Volume] [DECIMAL](18,6) NOT NULL,
	[ScrapeTimestampUtc] [DATETIME] NOT NULL,
	PRIMARY KEY CLUSTERED ([Ticker])
)

GO