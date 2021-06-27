USE Metis
GO

INSERT INTO [dbo].[UnitedStatesStockTickerMapping]
([iSharesTicker], [Nasdaq], [YahooFinance])
SELECT 'MOGA','MOG.A','MOG-A'
UNION ALL
SELECT 'JWA', 'JW.A', 'JW-A'
UNION ALL
SELECT 'BFB', 'BF.B', 'BF-B'
UNION ALL
SELECT 'BRKB', 'BRK.B', 'BRK-B'

GO