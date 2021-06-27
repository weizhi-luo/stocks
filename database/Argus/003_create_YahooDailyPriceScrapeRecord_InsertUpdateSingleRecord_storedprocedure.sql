USE Argus
GO

CREATE PROCEDURE dbo.YahooDailyPriceScrapeRecord_InsertUpdateSingleRecord
    @ticker VARCHAR(50),
	@include BIT,
	@benchmarkDate DATETIME NULL,
	@benchmarkOpen DECIMAL(18,6) NULL,
    @benchmarkHigh DECIMAL(18,6) NULL,
    @benchmarkLow DECIMAL(18,6) NULL,
    @benchmarkClose DECIMAL(18,6) NULL,
    @benchmarkAdjustedClose DECIMAL(18,6) NULL,
    @benchmarkVolume DECIMAL(18,6) NULL
AS
BEGIN
    IF EXISTS (SELECT 1 FROM [dbo].[YahooDailyPriceScrapeRecord] WHERE Ticker = @ticker)
	BEGIN
	    UPDATE [dbo].[YahooDailyPriceScrapeRecord]
		SET [BenchmarkDate] = @benchmarkDate,
		    [BenchmarkOpen] = @benchmarkOpen,
		    [BenchmarkHigh] = @benchmarkHigh,
		    [BenchmarkLow] = @benchmarkLow,
		    [BenchmarkClose] = @benchmarkClose,
		    [BenchmarkAdjustedClose] = @benchmarkAdjustedClose,
		    [BenchmarkVolume] = @benchmarkVolume
		WHERE Ticker = @ticker
	END
    ELSE
	BEGIN
	    INSERT INTO [dbo].[YahooDailyPriceScrapeRecord]
		(
		    [Ticker],
            [Include],
            [BenchmarkDate],
            [BenchmarkOpen],
            [BenchmarkHigh],
            [BenchmarkLow],
            [BenchmarkClose],
            [BenchmarkAdjustedClose],
            [BenchmarkVolume]
		)
		SELECT @ticker,
		    @include,
			@benchmarkDate,
			@benchmarkOpen,
			@benchmarkHigh,
			@benchmarkLow,
			@benchmarkClose,
			@benchmarkAdjustedClose,
			@benchmarkVolume
	END

END