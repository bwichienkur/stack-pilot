using MySqlConnector;
using StackPilot.Application.Connectors;

namespace StackPilot.Infrastructure.Connectors;

public class MySqlConnectorImpl : ConnectorBase
{
    private readonly MySqlDatabaseScanner _scanner;

    public MySqlConnectorImpl(MySqlDatabaseScanner scanner) => _scanner = scanner;

    public override string Type => "mysql";
    public override ConnectorCapabilities Capabilities => ConnectorCapabilities.DatabaseScan;

    public override async Task<ConnectionTestResult> TestConnectionAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var connectionString = context.Credentials.GetValueOrDefault("connection_string");
        if (string.IsNullOrWhiteSpace(connectionString))
            return new ConnectionTestResult { Success = false, Message = "Connection string is required" };

        try
        {
            await using var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync(ct);
            return new ConnectionTestResult { Success = true, Message = $"Connected to MySQL ({conn.Database})" };
        }
        catch (Exception ex)
        {
            return new ConnectionTestResult { Success = false, Message = $"MySQL connection failed: {ex.Message}" };
        }
    }

    public override async Task<SyncResult> SyncAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var config = ParseConfig(context.ConfigJson);
        var databases = config.GetValueOrDefault("databases", "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (databases.Length == 0)
            databases = [context.Credentials.GetValueOrDefault("database") ?? "mysql"];

        var scanned = 0;
        foreach (var db in databases)
        {
            var result = await _scanner.ScanAsync(context, db, ct);
            if (result.Tables.Count > 0) scanned++;
        }

        return new SyncResult
        {
            Success = true,
            ItemsProcessed = scanned,
            Metadata = new() { ["databases"] = databases, ["syncType"] = "database" }
        };
    }
}

public class MySqlDatabaseScanner : IDatabaseScanner
{
    public async Task<DatabaseScanResult> ScanAsync(ConnectorContext context, string databaseName, CancellationToken ct = default)
    {
        var connectionString = context.Credentials.GetValueOrDefault("connection_string");
        if (connectionString is null)
            return new DatabaseScanResult { DatabaseName = databaseName, RiskyPatterns = ["No connection string provided"] };

        var builder = new MySqlConnectionStringBuilder(connectionString) { Database = databaseName };
        var result = new DatabaseScanResult { DatabaseName = databaseName };

        try
        {
            await using var conn = new MySqlConnection(builder.ConnectionString);
            await conn.OpenAsync(ct);

            const string tableSql = """
                SELECT table_schema, table_name, table_rows
                FROM information_schema.tables
                WHERE table_schema = @schema AND table_type = 'BASE TABLE'
                ORDER BY table_name
                """;

            await using var cmd = new MySqlCommand(tableSql, conn);
            cmd.Parameters.AddWithValue("@schema", databaseName);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                var table = new TableInfo
                {
                    Schema = reader.GetString(0),
                    Name = reader.GetString(1),
                    RowCount = reader.IsDBNull(2) ? null : reader.GetInt64(2)
                };
                result.Tables.Add(table);
            }
            await reader.CloseAsync();

            foreach (var table in result.Tables)
            {
                table.Columns = await ScanColumnsAsync(conn, databaseName, table.Name, ct);
            }

            result.RiskyPatterns = DetectRiskyPatterns(result.Tables);
        }
        catch (Exception ex)
        {
            result.RiskyPatterns.Add($"Scan error: {ex.Message}");
        }

        return result;
    }

    private static async Task<List<ColumnInfo>> ScanColumnsAsync(MySqlConnection conn, string schema, string table, CancellationToken ct)
    {
        const string sql = """
            SELECT column_name, data_type, is_nullable,
                   CASE WHEN column_key = 'PRI' THEN true ELSE false END as is_pk,
                   CASE WHEN column_key = 'MUL' THEN true ELSE false END as is_fk
            FROM information_schema.columns
            WHERE table_schema = @schema AND table_name = @table
            ORDER BY ordinal_position
            """;

        var columns = new List<ColumnInfo>();
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@table", table);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            columns.Add(new ColumnInfo
            {
                Name = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetString(2).Equals("YES", StringComparison.OrdinalIgnoreCase),
                IsPrimaryKey = reader.GetBoolean(3),
                IsForeignKey = reader.GetBoolean(4)
            });
        }

        return columns;
    }

    private static List<string> DetectRiskyPatterns(List<TableInfo> tables)
    {
        var risks = new List<string>();
        foreach (var table in tables)
        {
            if (table.Columns.Any(c => c.Name.Contains("password", StringComparison.OrdinalIgnoreCase) && !c.Name.Contains("hash", StringComparison.OrdinalIgnoreCase)))
                risks.Add($"Table {table.Name} may store plaintext passwords");
            if (table.RowCount > 10_000_000)
                risks.Add($"Table {table.Name} has very high row count ({table.RowCount:N0})");
        }
        return risks;
    }
}
