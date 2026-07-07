using MongoDB.Bson;
using MongoDB.Driver;
using StackPilot.Application.Connectors;

namespace StackPilot.Infrastructure.Connectors;

public class MongoDbConnector : ConnectorBase
{
    private readonly MongoDbDatabaseScanner _scanner;

    public MongoDbConnector(MongoDbDatabaseScanner scanner) => _scanner = scanner;

    public override string Type => "mongodb";
    public override ConnectorCapabilities Capabilities => ConnectorCapabilities.DatabaseScan;

    public override async Task<ConnectionTestResult> TestConnectionAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var connectionString = context.Credentials.GetValueOrDefault("connection_string");
        if (string.IsNullOrWhiteSpace(connectionString))
            return new ConnectionTestResult { Success = false, Message = "MongoDB connection string is required" };

        try
        {
            var client = new MongoClient(connectionString);
            var adminDb = client.GetDatabase("admin");
            await adminDb.RunCommandAsync((Command<BsonDocument>)"{ping:1}", cancellationToken: ct);
            return new ConnectionTestResult { Success = true, Message = "Connected to MongoDB cluster" };
        }
        catch (Exception ex)
        {
            return new ConnectionTestResult { Success = false, Message = $"MongoDB connection failed: {ex.Message}" };
        }
    }

    public override async Task<SyncResult> SyncAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var config = ParseConfig(context.ConfigJson);
        var databases = config.GetValueOrDefault("databases", "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (databases.Length == 0)
            databases = ["admin"];

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

public class MongoDbDatabaseScanner : IDatabaseScanner
{
    public async Task<DatabaseScanResult> ScanAsync(ConnectorContext context, string databaseName, CancellationToken ct = default)
    {
        var connectionString = context.Credentials.GetValueOrDefault("connection_string");
        if (connectionString is null)
            return new DatabaseScanResult { DatabaseName = databaseName, RiskyPatterns = ["No connection string provided"] };

        var result = new DatabaseScanResult { DatabaseName = databaseName };

        try
        {
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            var collectionNames = await (await database.ListCollectionNamesAsync(cancellationToken: ct)).ToListAsync(ct);

            foreach (var collectionName in collectionNames)
            {
                var collection = database.GetCollection<BsonDocument>(collectionName);
                var count = await collection.EstimatedDocumentCountAsync(cancellationToken: ct);
                var sample = await collection.Find(FilterDefinition<BsonDocument>.Empty).Limit(1).FirstOrDefaultAsync(ct);

                var columns = new List<ColumnInfo>();
                if (sample is not null)
                {
                    foreach (var element in sample.Elements)
                    {
                        columns.Add(new ColumnInfo
                        {
                            Name = element.Name,
                            DataType = element.Value.BsonType.ToString(),
                            IsNullable = element.Value.IsBsonNull
                        });
                    }
                }

                result.Tables.Add(new TableInfo
                {
                    Schema = databaseName,
                    Name = collectionName,
                    RowCount = count,
                    Columns = columns
                });
            }

            if (result.Tables.Any(t => t.Columns.Any(c => c.Name.Contains("password", StringComparison.OrdinalIgnoreCase))))
                result.RiskyPatterns.Add("Collections may contain password fields");
        }
        catch (Exception ex)
        {
            result.RiskyPatterns.Add($"Scan error: {ex.Message}");
        }

        return result;
    }
}
