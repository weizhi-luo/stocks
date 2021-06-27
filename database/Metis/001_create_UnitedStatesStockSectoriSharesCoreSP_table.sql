USE Metis
GO

CREATE TABLE dbo.UnitedStatesStockSectoriSharesCoreSP
(
    [Id] [int] IDENTITY(1,1) NOT NULL,
    [Name] [varchar](200) NOT NULL,
    CONSTRAINT PK_UnitedStatesStockSectoriSharesCoreSP PRIMARY KEY (Id)
)

GO

CREATE UNIQUE NONCLUSTERED INDEX IX_UnitedStatesStockSectoriSharesCoreSP ON dbo.UnitedStatesStockSectoriSharesCoreSP
(
    [Name]
)

GO