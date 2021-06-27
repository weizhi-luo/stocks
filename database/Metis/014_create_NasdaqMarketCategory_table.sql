CREATE TABLE dbo.NasdaqMarketCategory
(
    [Id] [int] IDENTITY(1,1) NOT NULL,
	[Category] CHAR(1) NOT NULL,
	[Value] VARCHAR(200) NOT NULL,
	CONSTRAINT PK_NasdaqMarketCategory PRIMARY KEY (Id)
)
GO

CREATE UNIQUE NONCLUSTERED INDEX IX_NasdaqMarketCategory ON dbo.NasdaqMarketCategory
(
    [Category]
)
INCLUDE
(
    [Value]
)

GO