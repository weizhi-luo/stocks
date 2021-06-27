USE Metis
GO

CREATE PROCEDURE dbo.UnitedStatesStockTickeriSharesCoreSP500_InsertUpdate 
    @data dbo.UnitedStatesStockTickeriSharesCoreSPDataUdt READONLY
AS
BEGIN

    UPDATE t
    SET t.[Name] = s.[Name],
    t.[SectorId] = sec.[Id],
    t.[CUSIP] = s.[CUSIP],
    t.[ISIN] = s.[ISIN],
    t.[SEDOL] = s.[SEDOL],
    t.[ExchangeId] = ex.[Id],
    t.[ScrapeTimestampUtc] = s.[ScrapeTimestampUtc]
    FROM [dbo].[UnitedStatesStockTickeriSharesCoreSP500] t
    INNER JOIN @data s
    ON s.Ticker = t.Ticker
	LEFT JOIN [dbo].[UnitedStatesStockSectoriSharesCoreSP] sec
	ON sec.[Name] = s.Sector
	LEFT JOIN [dbo].[UnitedStatesStockExchangeiSharesCoreSP] ex
	ON ex.[Name] = s.Exchange
    WHERE t.ScrapeTimestampUtc < s.ScrapeTimestampUtc

    INSERT INTO [dbo].[UnitedStatesStockTickeriSharesCoreSP500]
    (
       [Ticker]
      ,[Name]
      ,[SectorId]
      ,[CUSIP]
      ,[ISIN]
      ,[SEDOL]
      ,[ExchangeId]
      ,[ScrapeTimestampUtc]
    )
    SELECT s.[Ticker]
      ,s.[Name]
      ,s.[Sector]
      ,s.[CUSIP]
      ,s.[ISIN]
      ,s.[SEDOL]
      ,s.[Exchange]
      ,s.[ScrapeTimestampUtc]
    FROM @data s
	LEFT JOIN [dbo].[UnitedStatesStockSectoriSharesCoreSP] sec
	ON sec.[Name] = s.[Sector]
	LEFT JOIN [dbo].[UnitedStatesStockExchangeiSharesCoreSP] ex
	ON ex.[Name] = s.[Exchange]
    WHERE NOT EXISTS (
        SELECT 1 FROM [dbo].[UnitedStatesStockTickeriSharesCoreSP500]
        WHERE Ticker = s.Ticker
    )

END