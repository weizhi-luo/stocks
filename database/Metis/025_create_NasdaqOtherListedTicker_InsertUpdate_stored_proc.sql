USE Metis
GO

CREATE PROCEDURE dbo.NasdaqOtherListedTicker_InsertUpdate 
    @data dbo.NasdaqOtherListedTickerDataUdt READONLY
AS
BEGIN

    UPDATE t
    SET t.[Ticker] = s.[Ticker],
	t.[Name] = s.[Name],
	t.[ExchangeId] = ex.Id,
	t.[CQSSymbol] = s.[CQSSymbol],
	t.[ETF] = s.[ETF],
	t.[RoundLotSize] = s.[RoundLotSize],
	t.[TestIssue] = s.[TestIssue],
	t.[NasdaqSymbol] = s.[NasdaqSymbol],
	t.[ScrapeTimestampUtc] = s.[ScrapeTimestampUtc]
    FROM [dbo].[NasdaqOtherListedTicker] t
    INNER JOIN @data s
    ON s.Ticker = t.Ticker
	LEFT JOIN [dbo].[NasdaqOtherListingExchange] ex
	ON ex.[Exchange] = s.[Exchange]
    WHERE t.ScrapeTimestampUtc < s.ScrapeTimestampUtc

    INSERT INTO [dbo].[NasdaqOtherListedTicker]
    (
	  [Ticker],
      [Name],
      [ExchangeId],
      [CQSSymbol],
      [ETF],
      [RoundLotSize],
      [TestIssue],
      [NasdaqSymbol],
      [ScrapeTimestampUtc]
    )
    SELECT s.[Ticker]
      ,s.[Name]
      ,ex.[Id]
      ,s.[CQSSymbol]
      ,s.[ETF]
      ,s.[RoundLotSize]
      ,s.[TestIssue]
      ,s.[NasdaqSymbol]
      ,s.[ScrapeTimestampUtc]
    FROM @data s
	LEFT JOIN [dbo].[NasdaqOtherListingExchange] ex
	ON ex.[Exchange] = s.[Exchange]
    WHERE NOT EXISTS (
        SELECT 1 FROM [dbo].[NasdaqOtherListedTicker]
        WHERE Ticker = s.Ticker
    )

END