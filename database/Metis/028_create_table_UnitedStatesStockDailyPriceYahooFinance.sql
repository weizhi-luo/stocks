USE Metis
GO

CREATE TABLE dbo.UnitedStatesStockDailyPriceYahooFinance (
    [Id] [BIGINT] IDENTITY(1,1) NOT NULL,
	[Ticker] [VARCHAR](50) NOT NULL,
	[Date] [DATE] NOT NULL,
	[Open] [DECIMAL](18,6) NULL,
	[High] [DECIMAL](18,6) NULL,
	[Low] [DECIMAL](18,6) NULL,
	[Close] [DECIMAL](18,6) NULL,
	[AdjustedClose] [DECIMAL](18,6) NULL,
	[Volume] [DECIMAL](18,6) NULL,
	[ScrapeTimestampUtc] [DATETIME] NOT NULL,
 CONSTRAINT [PK_UnitedStatesStockDailyPriceYahooFinance] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)
)

GO

CREATE NONCLUSTERED INDEX IX_UnitedStatesStockPriceYahooFinance_Ticker ON dbo.UnitedStatesStockDailyPriceYahooFinance
(
    [Ticker],
	[Date]
)
INCLUDE 
(
    [Open],
	[High],
	[Low],
	[Close],
	[AdjustedClose],
	[Volume],
	[ScrapeTimestampUtc]
)

GO