INSERT INTO dbo.NasdaqFinancialStatus
([Status], [Value])
SELECT 'D','Deficient: Issuer Failed to Meet NASDAQ Continued Listing Requirements'
UNION ALL
SELECT 'E','Delinquent: Issuer Missed Regulatory Filing Deadline'
UNION ALL
SELECT 'Q','Bankrupt: Issuer Has Filed for Bankruptcy'
UNION ALL
SELECT 'N','Normal (Default): Issuer Is NOT Deficient, Delinquent, or Bankrupt.'
UNION ALL
SELECT 'G','Deficient and Bankrupt'
UNION ALL
SELECT 'H','Deficient and Delinquent'
UNION ALL
SELECT 'J','Delinquent and Bankrupt'
UNION ALL
SELECT 'K','Deficient, Delinquent, and Bankrupt'