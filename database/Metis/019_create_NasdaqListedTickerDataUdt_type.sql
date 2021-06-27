USE Metis
GO

CREATE TYPE dbo.NasdaqListedTickerDataUdt AS TABLE 
(
    [Ticker] [varchar](50) NOT NULL,
	[Name] [varchar](500) NOT NULL,
	[MarketCategory] [char](1) NOT NULL,
	[TestIssue] [bit] NOT NULL,
	[FinancialStatus] [char](1) NOT NULL,
	[RoundLotSize] [int] NOT NULL,
	[ETF] [bit] NULL,
	[NextShares] [bit] NULL,
	[ScrapeTimestampUtc] [datetime] NOT NULL,
    PRIMARY KEY CLUSTERED ([Ticker])
)