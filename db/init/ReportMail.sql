

CREATE TABLE [ReportDefinition] (
  ReportDefinitionID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReportDefinitions PRIMARY KEY,
  ReportName   NVARCHAR(200) NOT NULL,
  Category     NVARCHAR(20)  NOT NULL,      -- line | bar | pie
  BaseKind     NVARCHAR(20)  NOT NULL,      -- sales | borrow | orders  ← 新增
  Description  NVARCHAR(1000) NULL,
  IsActive     BIT NOT NULL CONSTRAINT DF_ReportDefinitions_IsActive DEFAULT(1),
  IsSystem     BIT NOT NULL CONSTRAINT DF_ReportDefinitions_IsSystem DEFAULT(0),  -- //是否為預設報表
  SortOrder    INT NOT NULL CONSTRAINT DF_ReportDefinitions_SortOrder DEFAULT(0), -- //排序順序
  CreatedAt    DATETIME2(0) NOT NULL CONSTRAINT DF_ReportDefinitions_CreatedAt DEFAULT (GETDATE()),
  UpdatedAt    DATETIME2(0) NOT NULL CONSTRAINT DF_ReportDefinitions_UpdatedAt DEFAULT (GETDATE())
)

GO

CREATE TABLE [ReportFilter] (
  ReportFilterID       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReportFilters PRIMARY KEY,
  ReportDefinitionID   INT NOT NULL,
  FieldName            NVARCHAR(100) NOT NULL,   -- 例：OrderDate / BorrowDate / CategoryID / SalePrice / OrderStatus / OrderAmount / Metric
  DisplayName          NVARCHAR(200) NOT NULL,   -- 給 UI 顯示用
  DataType             NVARCHAR(20)  NOT NULL,   -- date/select/number/text...
  Operator             NVARCHAR(20)  NOT NULL,   -- between/in/eq/gt/lt...
  ValueJson            NVARCHAR(MAX) NOT NULL,   -- ← 用 JSON 儲值（取代 DefaultValue）
  Options              NVARCHAR(MAX) NULL,       -- 來源或選單的 JSON
  OrderIndex           INT NOT NULL CONSTRAINT DF_ReportFilters_OrderIndex DEFAULT(1),
  IsRequired           BIT NOT NULL CONSTRAINT DF_ReportFilters_IsRequired DEFAULT(0),
  IsActive             BIT NOT NULL CONSTRAINT DF_ReportFilters_IsActive DEFAULT(1),
  CreatedAt            DATETIME2(0) NOT NULL CONSTRAINT DF_ReportFilters_CreatedAt DEFAULT (GETDATE()),
  UpdatedAt            DATETIME2(0) NOT NULL CONSTRAINT DF_ReportFilters_UpdatedAt DEFAULT (GETDATE()),
  CONSTRAINT FK_ReportFilters_ReportDefinitions
    FOREIGN KEY (ReportDefinitionID) REFERENCES ReportDefinition(ReportDefinitionID)
    ON DELETE CASCADE
)

GO

CREATE TABLE [ReportExportLog] (
  [ExportID] int PRIMARY KEY,
  [UserID] int,
  [ReportName] nvarchar(100),
  [ExportFormat] nvarchar(20),
  [ExportAt] datetime2,
  [Filters] nvarchar(max),
  [FilePath] nvarchar(300)
)
GO


ALTER TABLE [ReportExportLog] ADD FOREIGN KEY ([UserID]) REFERENCES [USERS] ([UserID])
GO

