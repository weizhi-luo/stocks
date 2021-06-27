CREATE TABLE dbo.NasdaqListedTicker
(
	[Ticker] [varchar](50) NOT NULL,
	[Name] [varchar](500) NOT NULL,
	[MarketCategoryId] [int] NOT NULL,
	[TestIssue] [bit] NOT NULL,
	[FinancialStatusId] [int] NOT NULL,
	[RoundLotSize] [int] NOT NULL,
	[ETF] [bit] NULL,
	[NextShares] [bit] NULL,
	[ScrapeTimestampUtc] [datetime] NULL,
	FOREIGN KEY ([MarketCategoryId]) REFERENCES [dbo].[NasdaqMarketCategory](Id),
	FOREIGN KEY ([FinancialStatusId]) REFERENCES [dbo].[NasdaqFinancialStatus](Id)
)
GO

CREATE UNIQUE NONCLUSTERED INDEX IX_NasdaqListedTicker ON dbo.NasdaqListedTicker
(
    [Ticker]
)
INCLUDE
(
    ScrapeTimestampUtc
)

GO