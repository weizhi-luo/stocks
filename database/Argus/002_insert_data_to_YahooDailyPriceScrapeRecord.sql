USE Argus
GO

INSERT INTO dbo.YahooDailyPriceScrapeRecord
([Ticker], [Include])
select case 
           when m.YahooFinance is null then sp500.Ticker
		   else m.YahooFinance
		end as Ticker, 1
from Metis.[dbo].[UnitedStatesStockTickeriSharesCoreSP500] sp500
left join Metis.[dbo].[UnitedStatesStockTickerMapping] m
on sp500.Ticker = m.[iSharesTicker]

union 

select case 
           when m.YahooFinance is null then spmc.Ticker
		   else m.YahooFinance
		end as Ticker, 1
from Metis.[dbo].[UnitedStatesStockTickeriSharesCoreSPMidCap] spmc
left join Metis.[dbo].[UnitedStatesStockTickerMapping] m
on spmc.Ticker = m.[iSharesTicker]

union

select case 
           when m.YahooFinance is null then spsc.Ticker
		   else m.YahooFinance
		end as Ticker, 1
from Metis.[dbo].[UnitedStatesStockTickeriSharesCoreSPSmallCap] spsc
left join Metis.[dbo].[UnitedStatesStockTickerMapping] m
on spsc.Ticker = m.[iSharesTicker]