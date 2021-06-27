USE Metis
GO

INSERT INTO dbo.NasdaqMarketCategory (
    [Category], [Value]
)
SELECT 'Q', 'NASDAQ Global Select MarketSM'
UNION ALL
SELECT 'G', 'NASDAQ Global MarketSM'
UNION ALL
SELECT 'S', 'NASDAQ Capital Market'

GO