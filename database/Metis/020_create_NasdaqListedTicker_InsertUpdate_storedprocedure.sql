USE Metis
GO

CREATE PROCEDURE dbo.NasdaqListedTicker_InsertUpdate 
    @data dbo.NasdaqListedTickerDataUdt READONLY
AS
BEGIN

    UPDATE t
    SET t.[Ticker] = s.[Ticker],
	t.[Name] = s.[Name],
	t.[MarketCategoryId] = mc.Id,
	t.[TestIssue] = s.[TestIssue],
	t.[FinancialStatusId] = fs.Id,
	t.[RoundLotSize] = s.[RoundLotSize],
	t.[ETF] = s.[ETF],
	t.[NextShares] = s.[NextShares],
	t.[ScrapeTimestampUtc] = s.[ScrapeTimestampUtc]
    FROM [dbo].[NasdaqListedTicker] t
    INNER JOIN @data s
    ON s.Ticker = t.Ticker
	LEFT JOIN [dbo].[NasdaqMarketCategory] mc
	ON mc.[Category] = s.[MarketCategory]
	LEFT JOIN [dbo].[NasdaqFinancialStatus] fs
	ON fs.[Status] = s.[FinancialStatus]
    WHERE t.ScrapeTimestampUtc < s.ScrapeTimestampUtc

    INSERT INTO [dbo].[NasdaqListedTicker]
    (
       [Ticker],
	   [Name],
	   [MarketCategoryId],
	   [TestIssue],
	   [FinancialStatusId],
	   [RoundLotSize],
	   [ETF],
	   [NextShares],
	   [ScrapeTimestampUtc]
    )
    SELECT s.[Ticker]
      ,s.[Name]
      ,mc.[Id]
      ,s.[TestIssue]
      ,fs.[Id]
      ,s.[RoundLotSize]
      ,s.[ETF]
	  ,s.[NextShares]
      ,s.[ScrapeTimestampUtc]
    FROM @data s
	LEFT JOIN [dbo].[NasdaqMarketCategory] mc
	ON mc.[Category] = s.[MarketCategory]
	LEFT JOIN [dbo].[NasdaqFinancialStatus] fs
	ON fs.[Status] = s.[FinancialStatus]
    WHERE NOT EXISTS (
        SELECT 1 FROM [dbo].[NasdaqListedTicker]
        WHERE Ticker = s.Ticker
    )

END