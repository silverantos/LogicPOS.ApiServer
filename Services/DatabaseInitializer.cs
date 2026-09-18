using LogicPOS.ApiServer.Data;
using Microsoft.EntityFrameworkCore;

namespace LogicPOS.ApiServer.Services;

public sealed class DatabaseInitializer
{
    private static readonly MigrationBaseline[] KnownBaselines =
    [
        new("20260915114207_InitialCreate", ["ApiUsers"]),
        new("20260915193506_AddLicensingAndTerminals", ["ApiLicenses", "ApiTerminals"]),
        new("20260915200858_AddCompanyInfo", ["ApiCompanyInfos"])
    ];

    private readonly ApplicationDbContext _dbContext;
    private static readonly string[] LogicPosAnchorTables = ["Terminals", "Articles", "Documents"];
    private static readonly string[] LogicPosCoreTables =
    [
        "Terminals",
        "Articles",
        "Documents",
        "PaymentMethods",
        "DocumentTypes",
        "WorkSessionPeriods",
        "Warehouses",
        "FiscalYears",
        "Countries",
        "Currencies"
    ];

    public DatabaseInitializer(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DatabaseInitializationResult> InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (await UsesExistingLogicPosSchemaAsync(cancellationToken))
        {
            await CleanupApiArtifactsAsync(cancellationToken);
            return new DatabaseInitializationResult(true, false);
        }

        await BaselineEnsureCreatedDatabaseAsync(cancellationToken);
        await _dbContext.Database.MigrateAsync(cancellationToken);
        return new DatabaseInitializationResult(false, true);
    }

    private async Task<bool> UsesExistingLogicPosSchemaAsync(CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        try
        {
            var existingTables = await GetUserTableNamesAsync(connection, cancellationToken);
            var anchorCount = LogicPosAnchorTables.Count(existingTables.Contains);
            var coreCount = LogicPosCoreTables.Count(existingTables.Contains);

            return anchorCount >= 2 || (anchorCount >= 1 && coreCount >= 3);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private async Task CleanupApiArtifactsAsync(CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        try
        {
            var userTables = await GetUserTableNamesAsync(connection, cancellationToken);
            var apiTables = userTables
                .Where(static tableName => tableName.StartsWith("Api", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (var apiTable in apiTables)
            {
                await using var dropTableCommand = connection.CreateCommand();
                dropTableCommand.CommandText = $"DROP TABLE IF EXISTS {QuoteIdentifier(apiTable)};";
                await dropTableCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            if (!userTables.Contains("__EFMigrationsHistory"))
            {
                return;
            }

            var ownMigrationIds = _dbContext.Database.GetMigrations().ToHashSet(StringComparer.OrdinalIgnoreCase);
            var existingMigrationIds = await GetMigrationIdsAsync(connection, cancellationToken);
            if (existingMigrationIds.Count == 0 || existingMigrationIds.All(ownMigrationIds.Contains))
            {
                await using var dropHistoryCommand = connection.CreateCommand();
                dropHistoryCommand.CommandText = "DROP TABLE IF EXISTS __EFMigrationsHistory;";
                await dropHistoryCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private async Task BaselineEnsureCreatedDatabaseAsync(CancellationToken cancellationToken)
    {
        if (KnownBaselines.Length == 0)
        {
            return;
        }

        var connection = _dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        try
        {
            var existingTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var baseline in KnownBaselines)
            {
                foreach (var tableName in baseline.RequiredTables)
                {
                    if (await TableExistsAsync(connection, tableName, cancellationToken))
                    {
                        existingTables.Add(tableName);
                    }
                }
            }

            if (existingTables.Count == 0)
            {
                return;
            }

            await EnsureHistoryTableAsync(connection, cancellationToken);

            foreach (var baseline in KnownBaselines)
            {
                var tablesExist = baseline.RequiredTables.All(existingTables.Contains);
                var migrationExists = await MigrationExistsAsync(connection, baseline.MigrationId, cancellationToken);

                if (tablesExist && !migrationExists)
                {
                    await InsertMigrationHistoryAsync(connection, baseline.MigrationId, cancellationToken);
                    continue;
                }

                if (!tablesExist && migrationExists)
                {
                    await DeleteMigrationHistoryAsync(connection, baseline.MigrationId, cancellationToken);
                }
            }
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task EnsureHistoryTableAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken)
    {
        if (await TableExistsAsync(connection, "__EFMigrationsHistory", cancellationToken))
        {
            return;
        }

        await using var createHistoryCommand = connection.CreateCommand();
        createHistoryCommand.CommandText = @"
CREATE TABLE IF NOT EXISTS __EFMigrationsHistory (
    MigrationId TEXT NOT NULL CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY,
    ProductVersion TEXT NOT NULL
);";
        await createHistoryCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertMigrationHistoryAsync(System.Data.Common.DbConnection connection, string migrationId, CancellationToken cancellationToken)
    {
        await using var insertHistoryCommand = connection.CreateCommand();
        insertHistoryCommand.CommandText = @"
INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
VALUES ($migrationId, $productVersion);";

        var migrationIdParameter = insertHistoryCommand.CreateParameter();
        migrationIdParameter.ParameterName = "$migrationId";
        migrationIdParameter.Value = migrationId;
        insertHistoryCommand.Parameters.Add(migrationIdParameter);

        var productVersionParameter = insertHistoryCommand.CreateParameter();
        productVersionParameter.ParameterName = "$productVersion";
        productVersionParameter.Value = "9.0.9";
        insertHistoryCommand.Parameters.Add(productVersionParameter);

        await insertHistoryCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteMigrationHistoryAsync(System.Data.Common.DbConnection connection, string migrationId, CancellationToken cancellationToken)
    {
        await using var deleteHistoryCommand = connection.CreateCommand();
        deleteHistoryCommand.CommandText = "DELETE FROM __EFMigrationsHistory WHERE MigrationId = $migrationId;";

        var migrationIdParameter = deleteHistoryCommand.CreateParameter();
        migrationIdParameter.ParameterName = "$migrationId";
        migrationIdParameter.Value = migrationId;
        deleteHistoryCommand.Parameters.Add(migrationIdParameter);

        await deleteHistoryCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> TableExistsAsync(System.Data.Common.DbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $tableName;";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "$tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result) > 0;
    }

    private static async Task<bool> MigrationExistsAsync(System.Data.Common.DbConnection connection, string migrationId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = $migrationId;";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "$migrationId";
        parameter.Value = migrationId;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result) > 0;
    }

    private static async Task<HashSet<string>> GetUserTableNamesAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";

        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static async Task<List<string>> GetMigrationIdsAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory;";

        var migrations = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            migrations.Add(reader.GetString(0));
        }

        return migrations;
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private sealed record MigrationBaseline(string MigrationId, string[] RequiredTables);

    public sealed record DatabaseInitializationResult(bool UsesExistingLogicPosSchema, bool MigrationsApplied);
}
