CREATE TABLE dbo.NasdaqFinancialStatus
(
    [Id] [int] IDENTITY(1,1) NOT NULL,
	[Status] CHAR(1) NOT NULL,
	[Value] VARCHAR(200) NOT NULL,
	CONSTRAINT PK_NasdaqFinancialStatus PRIMARY KEY (Id)
)
GO

CREATE UNIQUE NONCLUSTERED INDEX IX_NasdaqFinancialStatus ON dbo.NasdaqFinancialStatus
(
    [Status]
)
INCLUDE
(
    [Value]
)

GO