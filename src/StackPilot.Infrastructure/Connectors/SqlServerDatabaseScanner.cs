using Microsoft.Data.SqlClient;
using StackPilot.Application.Connectors;

namespace StackPilot.Infrastructure.Connectors;

public class SqlServerDatabaseScanner : IDatabaseScanner
{
    public async Task<DatabaseScanResult> ScanAsync(ConnectorContext context, string databaseName, CancellationToken ct = default)
    {
        var connectionString = context.Credentials.GetValueOrDefault("connection_string");
        if (connectionString is null)
            return new DatabaseScanResult { DatabaseName = databaseName, RiskyPatterns = ["No connection string provided"] };

        var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = databaseName };
        var result = new DatabaseScanResult { DatabaseName = databaseName };

        try
        {
            await using var conn = new SqlConnection(builder.ConnectionString);
            await conn.OpenAsync(ct);

            result.Tables = await ScanTablesAsync(conn, ct);
            result.Views = await ScanViewsAsync(conn, ct);
            result.StoredProcedures = await ScanProceduresAsync(conn, ct);
            result.Relationships = await ScanRelationshipsAsync(conn, ct);
            result.RiskyPatterns = DetectRiskyPatterns(result.Tables);
        }
        catch (Exception ex)
        {
            result.RiskyPatterns.Add($"Scan error: {ex.Message}");
        }

        return result;
    }

    private static async Task<List<TableInfo>> ScanTablesAsync(SqlConnection conn, CancellationToken ct)
    {
        const string sql = """
            SELECT TABLE_SCHEMA, TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA NOT IN ('sys')
            ORDER BY TABLE_SCHEMA, TABLE_NAME
            """;

        var tables = new List<TableInfo>();
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var tableList = new List<(string Schema, string Name)>();
        while (await reader.ReadAsync(ct))
            tableList.Add((reader.GetString(0), reader.GetString(1)));
        await reader.CloseAsync();

        foreach (var (schema, name) in tableList)
        {
            tables.Add(new TableInfo
            {
                Schema = schema,
                Name = name,
                Columns = await ScanColumnsAsync(conn, schema, name, ct),
                Indexes = await ScanIndexesAsync(conn, schema, name, ct)
            });
        }
        return tables;
    }

    private static async Task<List<ColumnInfo>> ScanColumnsAsync(SqlConnection conn, string schema, string table, CancellationToken ct)
    {
        const string sql = """
            SELECT c.COLUMN_NAME, c.DATA_TYPE, c.IS_NULLABLE,
                   CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END,
                   CASE WHEN fk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.COLUMN_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                WHERE tc.TABLE_SCHEMA = @schema AND tc.TABLE_NAME = @table AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ) pk ON c.COLUMN_NAME = pk.COLUMN_NAME
            LEFT JOIN (
                SELECT ku.COLUMN_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                WHERE tc.TABLE_SCHEMA = @schema AND tc.TABLE_NAME = @table AND tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
            ) fk ON c.COLUMN_NAME = fk.COLUMN_NAME
            WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @table
            ORDER BY c.ORDINAL_POSITION
            """;

        var columns = new List<ColumnInfo>();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@table", table);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            columns.Add(new ColumnInfo
            {
                Name = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetString(2) == "YES",
                IsPrimaryKey = reader.GetInt32(3) == 1,
                IsForeignKey = reader.GetInt32(4) == 1
            });
        }
        return columns;
    }

    private static async Task<List<string>> ScanIndexesAsync(SqlConnection conn, string schema, string table, CancellationToken ct)
    {
        const string sql = """
            SELECT i.name FROM sys.indexes i
            JOIN sys.tables t ON i.object_id = t.object_id
            JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE s.name = @schema AND t.name = @table AND i.name IS NOT NULL
            """;
        var indexes = new List<string>();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@table", table);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            indexes.Add(reader.GetString(0));
        return indexes;
    }

    private static async Task<List<ViewInfo>> ScanViewsAsync(SqlConnection conn, CancellationToken ct)
    {
        const string sql = "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.VIEWS";
        var views = new List<ViewInfo>();
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            views.Add(new ViewInfo { Schema = reader.GetString(0), Name = reader.GetString(1) });
        return views;
    }

    private static async Task<List<StoredProcedureInfo>> ScanProceduresAsync(SqlConnection conn, CancellationToken ct)
    {
        const string sql = """
            SELECT ROUTINE_SCHEMA, ROUTINE_NAME FROM INFORMATION_SCHEMA.ROUTINES
            WHERE ROUTINE_TYPE = 'PROCEDURE'
            """;
        var procs = new List<StoredProcedureInfo>();
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            procs.Add(new StoredProcedureInfo { Schema = reader.GetString(0), Name = reader.GetString(1) });
        return procs;
    }

    private static async Task<List<RelationshipInfo>> ScanRelationshipsAsync(SqlConnection conn, CancellationToken ct)
    {
        const string sql = """
            SELECT OBJECT_SCHEMA_NAME(fk.parent_object_id) + '.' + OBJECT_NAME(fk.parent_object_id),
                   c1.name, OBJECT_SCHEMA_NAME(fk.referenced_object_id) + '.' + OBJECT_NAME(fk.referenced_object_id), c2.name
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            JOIN sys.columns c1 ON fkc.parent_object_id = c1.object_id AND fkc.parent_column_id = c1.column_id
            JOIN sys.columns c2 ON fkc.referenced_object_id = c2.object_id AND fkc.referenced_column_id = c2.column_id
            """;
        var rels = new List<RelationshipInfo>();
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rels.Add(new RelationshipInfo
            {
                FromTable = reader.GetString(0),
                FromColumn = reader.GetString(1),
                ToTable = reader.GetString(2),
                ToColumn = reader.GetString(3)
            });
        }
        return rels;
    }

    private static List<string> DetectRiskyPatterns(List<TableInfo> tables)
    {
        var risks = new List<string>();
        foreach (var table in tables)
        {
            if (!table.Columns.Any(c => c.Name is "CreatedAt" or "created_at" or "CreatedOn"))
                risks.Add($"Table {table.Schema}.{table.Name} has no audit timestamp column");
        }
        return risks;
    }
}
