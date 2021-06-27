USE Metis
GO

CREATE TYPE dbo.NasdaqOtherListedTickerDataUdt AS TABLE 
(
    [Ticker] [varchar](50) NOT NULL,
	[Name] [varchar](500) NOT NULL,
	[Exchange] [char](1) NOT NULL,
	[CQSSymbol] [varchar](50) NOT NULL,
	[ETF] [bit] NULL,
	[RoundLotSize] [int] NOT NULL,
	[TestIssue] [bit] NOT NULL,
	[NasdaqSymbol] [varchar](50) NOT NULL,
	[ScrapeTimestampUtc] [datetime] NOT NULL,
    PRIMARY KEY CLUSTERED ([Ticker])
)

GO