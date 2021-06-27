USE Metis
GO

CREATE TABLE dbo.NasdaqOtherListingExchange
(
    [Id] [int] IDENTITY(1,1) NOT NULL,
	[Exchange] CHAR(1) NOT NULL,
	[Value] VARCHAR(200) NOT NULL,
	CONSTRAINT PK_NasdaqOtherListingExchange PRIMARY KEY (Id)
)
GO

CREATE UNIQUE NONCLUSTERED INDEX IX_NasdaqOtherListingExchange ON dbo.NasdaqOtherListingExchange
(
    [Exchange]
)
INCLUDE
(
    [Value]
)

GO