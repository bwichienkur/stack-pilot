using Npgsql;
using StackPilot.Application.Connectors;

namespace StackPilot.Infrastructure.Connectors;

public class PostgreSqlDatabaseScanner : IDatabaseScanner
{
    public async Task<DatabaseScanResult> ScanAsync(ConnectorContext context, string databaseName, CancellationToken ct = default)
    {
        var connectionString = context.Credentials.GetValueOrDefault("connection_string");
        if (connectionString is null)
            return EmptyResult(databaseName, "No connection string provided");

        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Database = databaseName };
        var result = new DatabaseScanResult { DatabaseName = databaseName };

        try
        {
            await using var conn = new NpgsqlConnection(builder.ConnectionString);
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

    private static async Task<List<TableInfo>> ScanTablesAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        const string sql = """
            SELECT t.table_schema, t.table_name,
                   (SELECT reltuples::bigint FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE c.relname = t.table_name AND n.nspname = t.table_schema LIMIT 1) as row_count
            FROM information_schema.tables t
            WHERE t.table_schema NOT IN ('pg_catalog', 'information_schema')
              AND t.table_type = 'BASE TABLE'
            ORDER BY t.table_schema, t.table_name
            """;

        var tables = new List<TableInfo>();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var tableList = new List<(string Schema, string Name, long? RowCount)>();
        while (await reader.ReadAsync(ct))
            tableList.Add((reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetInt64(2)));
        await reader.CloseAsync();

        foreach (var (schema, name, rowCount) in tableList)
        {
            var table = new TableInfo { Schema = schema, Name = name, RowCount = rowCount };
            table.Columns = await ScanColumnsAsync(conn, schema, name, ct);
            table.Indexes = await ScanIndexesAsync(conn, schema, name, ct);
            tables.Add(table);
        }
        return tables;
    }

    private static async Task<List<ColumnInfo>> ScanColumnsAsync(NpgsqlConnection conn, string schema, string table, CancellationToken ct)
    {
        const string sql = """
            SELECT c.column_name, c.data_type, c.is_nullable,
                   CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END as is_pk,
                   CASE WHEN fk.column_name IS NOT NULL THEN true ELSE false END as is_fk
            FROM information_schema.columns c
            LEFT JOIN (
                SELECT kcu.column_name FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name
                WHERE tc.table_schema = @schema AND tc.table_name = @table AND tc.constraint_type = 'PRIMARY KEY'
            ) pk ON c.column_name = pk.column_name
            LEFT JOIN (
                SELECT kcu.column_name FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name
                WHERE tc.table_schema = @schema AND tc.table_name = @table AND tc.constraint_type = 'FOREIGN KEY'
            ) fk ON c.column_name = fk.column_name
            WHERE c.table_schema = @schema AND c.table_name = @table
            ORDER BY c.ordinal_position
            """;

        var columns = new List<ColumnInfo>();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            columns.Add(new ColumnInfo
            {
                Name = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetString(2) == "YES",
                IsPrimaryKey = reader.GetBoolean(3),
                IsForeignKey = reader.GetBoolean(4)
            });
        }
        return columns;
    }

    private static async Task<List<string>> ScanIndexesAsync(NpgsqlConnection conn, string schema, string table, CancellationToken ct)
    {
        const string sql = """
            SELECT indexname FROM pg_indexes
            WHERE schemaname = @schema AND tablename = @table
            """;
        var indexes = new List<string>();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            indexes.Add(reader.GetString(0));
        return indexes;
    }

    private static async Task<List<ViewInfo>> ScanViewsAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        const string sql = """
            SELECT table_schema, table_name FROM information_schema.views
            WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
            """;
        var views = new List<ViewInfo>();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            views.Add(new ViewInfo { Schema = reader.GetString(0), Name = reader.GetString(1) });
        return views;
    }

    private static async Task<List<StoredProcedureInfo>> ScanProceduresAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        const string sql = """
            SELECT routine_schema, routine_name FROM information_schema.routines
            WHERE routine_schema NOT IN ('pg_catalog', 'information_schema') AND routine_type = 'FUNCTION'
            """;
        var procs = new List<StoredProcedureInfo>();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            procs.Add(new StoredProcedureInfo { Schema = reader.GetString(0), Name = reader.GetString(1) });
        return procs;
    }

    private static async Task<List<RelationshipInfo>> ScanRelationshipsAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        const string sql = """
            SELECT tc.table_schema || '.' || tc.table_name, kcu.column_name,
                   ccu.table_schema || '.' || ccu.table_name, ccu.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name
            JOIN information_schema.constraint_column_usage ccu ON ccu.constraint_name = tc.constraint_name
            WHERE tc.constraint_type = 'FOREIGN KEY'
            """;
        var rels = new List<RelationshipInfo>();
        await using var cmd = new NpgsqlCommand(sql, conn);
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
            if (!table.Columns.Any(c => c.Name is "created_at" or "CreatedAt" or "created_on"))
                risks.Add($"Table {table.Schema}.{table.Name} has no created timestamp column");
            if (table.Columns.Any(c => c.Name.Contains("password", StringComparison.OrdinalIgnoreCase) && c.DataType.Contains("varchar")))
                risks.Add($"Table {table.Schema}.{table.Name} may store passwords in plain text column");
        }
        return risks;
    }

    private static DatabaseScanResult EmptyResult(string db, string reason) => new()
    {
        DatabaseName = db,
        RiskyPatterns = [reason]
    };
}
