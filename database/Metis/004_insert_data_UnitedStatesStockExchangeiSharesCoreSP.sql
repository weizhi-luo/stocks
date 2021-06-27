USE Metis
GO

INSERT INTO dbo.UnitedStatesStockExchangeiSharesCoreSP
([Name])
SELECT 'Cboe BZX formerly known as BATS'
UNION ALL
SELECT 'NASDAQ'
UNION ALL
SELECT 'New York Stock Exchange Inc.'
UNION ALL
SELECT 'Nyse Mkt Llc'