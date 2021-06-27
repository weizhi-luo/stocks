USE Metis
GO

CREATE TABLE dbo.UnitedStatesStockTickeriSharesCoreSPMidCap
(
    [Ticker] [varchar](50) NOT NULL,
    [Name] [varchar](500) NOT NULL,
    [SectorId] [int] NOT NULL,
    [CUSIP] [varchar](50) NOT NULL,
    [ISIN] [varchar](50) NOT NULL,
    [SEDOL] [varchar](50) NOT NULL,
    [ExchangeId] [int] NOT NULL,
    [ScrapeTimestampUtc] [datetime] NOT NULL,
	FOREIGN KEY ([SectorId]) REFERENCES dbo.UnitedStatesStockSectoriSharesCoreSP(Id),
	FOREIGN KEY ([ExchangeId]) REFERENCES dbo.UnitedStatesStockExchangeiSharesCoreSP(Id)
)

GO

CREATE UNIQUE NONCLUSTERED INDEX IX_UnitedStatesStockTickeriSharesCoreSPMidCap_Ticker ON dbo.UnitedStatesStockTickeriSharesCoreSPMidCap
(
    [Ticker]
)
INCLUDE
(
    ScrapeTimestampUtc
)

GO