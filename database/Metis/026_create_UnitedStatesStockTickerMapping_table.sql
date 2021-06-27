USE [Metis]
GO

CREATE TABLE [dbo].[UnitedStatesStockTickerMapping](
	[iSharesTicker] [varchar](50) NOT NULL,
	[Nasdaq] [varchar](50) NOT NULL,
	[YahooFinance] [varchar](50) NOT NULL
)
GO

CREATE NONCLUSTERED INDEX IX_UnitedStatesStockTickerMapping_YahooFinance ON [dbo].[UnitedStatesStockTickerMapping] (
    [YahooFinance]
)
INCLUDE (
    [Nasdaq],
	[iSharesTicker]
)

GO