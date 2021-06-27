USE Metis
GO

INSERT INTO dbo.NasdaqOtherListingExchange
([Exchange],[Value])
SELECT 'A','NYSE MKT'
UNION ALL
SELECT 'N','New York Stock Exchange (NYSE)'
UNION ALL
SELECT 'P','NYSE ARCA'
UNION ALL
SELECT 'Z','BATS Global Markets (BATS)'
UNION ALL
SELECT 'V','Investors'' Exchange, LLC (IEXG)'

GO