USE Metis
GO

CREATE TYPE dbo.UnitedStatesStockTickeriSharesCoreSPDataUdt AS TABLE 
(
    [Ticker] [varchar](50) NOT NULL,
    [Name] [varchar](500) NOT NULL,
    [Sector] [varchar](200) NOT NULL,
    [CUSIP] [varchar](50) NOT NULL,
    [ISIN] [varchar](50) NOT NULL,
    [SEDOL] [varchar](50) NOT NULL,
    [Exchange] [varchar](200) NOT NULL,
    [ScrapeTimestampUtc] [datetime] NOT NULL,
    PRIMARY KEY CLUSTERED ([Ticker])
)