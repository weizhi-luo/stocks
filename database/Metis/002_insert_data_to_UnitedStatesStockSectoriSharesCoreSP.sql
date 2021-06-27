USE Metis
GO

INSERT INTO dbo.UnitedStatesStockSectoriSharesCoreSP
([Name])
SELECT 'Communication'
UNION ALL
SELECT 'Consumer Discretionary'
UNION ALL
SELECT 'Consumer Staples'
UNION ALL
SELECT 'Energy'
UNION ALL
SELECT 'Financials'
UNION ALL
SELECT 'Health Care'
UNION ALL
SELECT 'Industrials'
UNION ALL
SELECT 'Information Technology'
UNION ALL
SELECT 'Materials'
UNION ALL
SELECT 'Real Estate'
UNION ALL
SELECT 'Utilities'