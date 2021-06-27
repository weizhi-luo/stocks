USE Metis
GO

CREATE TABLE dbo.UnitedStatesStockExchangeiSharesCoreSP
(
    [Id] [int] IDENTITY(1,1) NOT NULL,
    [Name] [varchar](200) NOT NULL,
    CONSTRAINT PK_UnitedStatesStockExchangeiSharesCoreSP PRIMARY KEY (Id)
)

GO

CREATE UNIQUE NONCLUSTERED INDEX IX_UnitedStatesStockExchangeiSharesCoreSP ON dbo.UnitedStatesStockExchangeiSharesCoreSP
(
    [Name]
)

GO