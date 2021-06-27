USE Metis
GO

CREATE TABLE dbo.NasdaqOtherListedTicker
(
	[Ticker] [varchar](50) NOT NULL,
	[Name] [varchar](500) NOT NULL,
	[ExchangeId] [int] NOT NULL,
	[CQSSymbol] [varchar](50) NOT NULL,
	[ETF] [bit] NULL,
	[RoundLotSize] [int] NOT NULL,
	[TestIssue] [bit] NOT NULL,
	[NasdaqSymbol] [varchar](50) NOT NULL,
	[ScrapeTimestampUtc] [datetime] NOT NULL,
	FOREIGN KEY ([ExchangeId]) REFERENCES [dbo].[NasdaqOtherListingExchange](Id)
)
GO

CREATE UNIQUE NONCLUSTERED INDEX IX_NasdaqOtherListedTicker ON dbo.NasdaqOtherListedTicker
(
    [Ticker]
)
INCLUDE
(
    ScrapeTimestampUtc
)

GO