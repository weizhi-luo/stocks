USE Metis
GO

CREATE PROCEDURE dbo.UnitedStatesStockDailyPriceYahooFinance_InsertUpdate
    @data dbo.UnitedStatesStockDailyPriceDataUtc READONLY
AS
BEGIN

	UPDATE t
	SET t.[Open] = s.[Open],
	t.[High] = s.[High],
	t.[Low] = s.[Low],
	t.[Close] = s.[Close],
	t.[AdjustedClose] = s.[AdjustedClose],
	t.[Volume] = s.[Volume],
	t.[ScrapeTimestampUtc] = s.[ScrapeTimestampUtc]
	FROM dbo.UnitedStatesStockDailyPriceYahooFinance t
	INNER JOIN @data s
	ON s.[Ticker] = t.[Ticker]
	AND s.[Date] = t.[Date]
	WHERE t.ScrapeTimestampUtc < s.ScrapeTimestampUtc

	INSERT INTO dbo.UnitedStatesStockDailyPriceYahooFinance
	(
	  [Ticker],
	  [Date],
	  [Open],
	  [High],
	  [Low],
	  [Close],
	  [AdjustedClose],
	  [Volume],
	  [ScrapeTimestampUtc]
	)
	SELECT s.[Ticker],
	  s.[Date],
	  s.[Open],
	  s.[High],
	  s.[Low],
	  s.[Close],
	  s.[AdjustedClose],
	  s.[Volume],
	  s.[ScrapeTimestampUtc]
	FROM @data s
	WHERE NOT EXISTS (
	    SELECT 1 FROM dbo.UnitedStatesStockDailyPriceYahooFinance
		WHERE [Ticker] = s.[Ticker]
		AND [Date] = s.[Date]
	)

END