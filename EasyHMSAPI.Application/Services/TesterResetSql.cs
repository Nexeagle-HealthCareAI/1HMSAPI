using EasyHMSAPI.Domain.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>One column of a foreign key: [ChildSchema].[ChildTable].[ChildColumn] points at [ParentSchema].[ParentTable].[ParentColumn].</summary>
    [ExcludeFromCodeCoverage]
    public class TesterResetForeignKeyColumn
    {
        public string FkName { get; set; } = string.Empty;
        public string ChildSchema { get; set; } = string.Empty;
        public string ChildTable { get; set; } = string.Empty;
        public string ChildColumn { get; set; } = string.Empty;
        public string ParentSchema { get; set; } = string.Empty;
        public string ParentTable { get; set; } = string.Empty;
        public string ParentColumn { get; set; } = string.Empty;
        public int Ordinal { get; set; }
    }

    /// <summary>A table that carries a hospital id column.</summary>
    [ExcludeFromCodeCoverage]
    public class TesterResetTableColumn
    {
        public string SchemaName { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string ColumnName { get; set; } = string.Empty;
    }

    /// <summary>
    /// The database calls the tester reset makes. Behind an interface so the delete order and the safety rules
    /// (ResetTesterAccountHandler) are unit tested without a SQL Server.
    /// </summary>
    public interface ITesterResetSql
    {
        Task<List<TesterResetForeignKeyColumn>> ForeignKeysAsync(CancellationToken cancellationToken);
        /// <summary>Every table with a uniqueidentifier column called HospitalId (any letter case).</summary>
        Task<List<TesterResetTableColumn>> HospitalTablesAsync(CancellationToken cancellationToken);
        /// <summary>Runs a "SELECT COUNT(*) AS [Value] ..." statement.</summary>
        Task<int> CountAsync(string sql, CancellationToken cancellationToken);
        /// <summary>Runs a DELETE and returns the rows it removed.</summary>
        Task<int> ExecuteAsync(string sql, CancellationToken cancellationToken);
        Task BeginAsync(CancellationToken cancellationToken);
        Task CommitAsync(CancellationToken cancellationToken);
        Task RollbackAsync(CancellationToken cancellationToken);
    }

    /// <summary>SQL Server implementation over the app's own connection (and its transaction).</summary>
    [ExcludeFromCodeCoverage]
    public class TesterResetSql : ITesterResetSql
    {
        private const string ForeignKeysSql = @"
SELECT fk.name AS FkName,
       cs.name AS ChildSchema, ctab.name AS ChildTable, ccol.name AS ChildColumn,
       ps.name AS ParentSchema, ptab.name AS ParentTable, pcol.name AS ParentColumn,
       fkc.constraint_column_id AS Ordinal
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables ctab ON ctab.object_id = fk.parent_object_id
JOIN sys.schemas cs ON cs.schema_id = ctab.schema_id
JOIN sys.columns ccol ON ccol.object_id = fkc.parent_object_id AND ccol.column_id = fkc.parent_column_id
JOIN sys.tables ptab ON ptab.object_id = fk.referenced_object_id
JOIN sys.schemas ps ON ps.schema_id = ptab.schema_id
JOIN sys.columns pcol ON pcol.object_id = fkc.referenced_object_id AND pcol.column_id = fkc.referenced_column_id";

        private const string HospitalTablesSql = @"
SELECT s.name AS SchemaName, t.name AS TableName, c.name AS ColumnName
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.columns c ON c.object_id = t.object_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE LOWER(c.name) = 'hospitalid' AND ty.name = 'uniqueidentifier'";

        private readonly AppDbContext _context;
        private IDbContextTransaction? _transaction;

        public TesterResetSql(AppDbContext context)
        {
            _context = context;
        }

        public Task<List<TesterResetForeignKeyColumn>> ForeignKeysAsync(CancellationToken cancellationToken) =>
            _context.Database.SqlQueryRaw<TesterResetForeignKeyColumn>(ForeignKeysSql).ToListAsync(cancellationToken);

        public Task<List<TesterResetTableColumn>> HospitalTablesAsync(CancellationToken cancellationToken) =>
            _context.Database.SqlQueryRaw<TesterResetTableColumn>(HospitalTablesSql).ToListAsync(cancellationToken);

        public Task<int> CountAsync(string sql, CancellationToken cancellationToken) =>
            _context.Database.SqlQueryRaw<int>(sql).SingleAsync(cancellationToken);

        public Task<int> ExecuteAsync(string sql, CancellationToken cancellationToken) =>
            _context.Database.ExecuteSqlRawAsync(sql, cancellationToken);

        public async Task BeginAsync(CancellationToken cancellationToken)
        {
            _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        }

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            if (_transaction == null) return;
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        public async Task RollbackAsync(CancellationToken cancellationToken)
        {
            if (_transaction == null) return;
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}
