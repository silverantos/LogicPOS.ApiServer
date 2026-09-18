using LogicPOS.ApiServer.Data;
using LogicPOS.ApiServer.Data.Entities;
using LogicPOS.ApiServer.DTOs;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;
using System.Globalization;

namespace LogicPOS.ApiServer.Services;

public sealed class TerminalService
{
    private const string TerminalsTableName = "Terminals";

    private readonly ApplicationDbContext _dbContext;

    public TerminalService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<IReadOnlyList<TerminalResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return UseOpenConnectionAsync(async connection =>
        {
            var schema = await GetTerminalSchemaAsync(connection, cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = BuildSelectAllSql(schema);

            var terminals = await ReadTerminalsAsync(command, cancellationToken);
            return (IReadOnlyList<TerminalResponse>)terminals.Select(Map).ToList();
        }, cancellationToken);
    }

    public Task<TerminalResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return UseOpenConnectionAsync(async connection =>
        {
            var schema = await GetTerminalSchemaAsync(connection, cancellationToken);
            var terminal = await FindTerminalByColumnAsync(connection, schema, "Id", id, cancellationToken);
            return terminal is null ? null : Map(terminal);
        }, cancellationToken);
    }

    public Task<TerminalResponse?> GetByHardwareIdAsync(string hardwareId, CancellationToken cancellationToken = default)
    {
        return UseOpenConnectionAsync(async connection =>
        {
            var schema = await GetTerminalSchemaAsync(connection, cancellationToken);
            var terminal = await FindTerminalByColumnAsync(connection, schema, "HardwareId", hardwareId, cancellationToken);
            return terminal is null ? null : Map(terminal);
        }, cancellationToken);
    }

    public Task<AddEntityIdResponse> CreateAsync(CreateTerminalRequest request, CancellationToken cancellationToken = default)
    {
        return UseOpenConnectionAsync(async connection =>
        {
            var schema = await GetTerminalSchemaAsync(connection, cancellationToken);
            var hardwareId = request.HardwareId.Trim();

            var existing = await FindTerminalByColumnAsync(connection, schema, "HardwareId", hardwareId, cancellationToken);
            if (existing is not null)
            {
                return new AddEntityIdResponse { Id = existing.Id };
            }

            var count = await CountTerminalsAsync(connection, cancellationToken);
            var utcNow = DateTime.UtcNow;
            var terminal = new ApiTerminal
            {
                Id = Guid.NewGuid(),
                Order = (uint)(count + 1),
                Code = $"T{count + 1}",
                Designation = $"Terminal {count + 1}",
                HardwareId = hardwareId,
                TimerInterval = 100,
                IsDefault = count == 0,
                CreatedUtc = utcNow,
                UpdatedUtc = utcNow
            };

            try
            {
                await InsertTerminalAsync(connection, schema, terminal, cancellationToken);
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 19
                && exception.Message.Contains("Terminals.HardwareId", StringComparison.OrdinalIgnoreCase))
            {
                var concurrentExisting = await FindTerminalByColumnAsync(connection, schema, "HardwareId", hardwareId, cancellationToken);
                if (concurrentExisting is not null)
                {
                    return new AddEntityIdResponse { Id = concurrentExisting.Id };
                }

                throw;
            }

            return new AddEntityIdResponse
            {
                Id = terminal.Id
            };
        }, cancellationToken);
    }

    private async Task<TResult> UseOpenConnectionAsync<TResult>(Func<DbConnection, Task<TResult>> action, CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            return await action(connection);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<TerminalTableSchema> GetTerminalSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({QuoteIdentifier(TerminalsTableName)});";

        var columns = new List<TerminalColumnInfo>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(new TerminalColumnInfo(
                reader.GetString(1),
                Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture) != 0,
                reader.IsDBNull(4) ? null : reader.GetString(4),
                Convert.ToInt64(reader.GetValue(5), CultureInfo.InvariantCulture) != 0));
        }

        if (columns.Count == 0)
        {
            throw new InvalidOperationException("The LogicPOS 'Terminals' table was not found.");
        }

        return new TerminalTableSchema(columns);
    }

    private static string BuildSelectAllSql(TerminalTableSchema schema)
    {
        var orderColumns = new List<string>();
        if (schema.TryGetColumn("IsDefault", out var isDefaultColumn))
        {
            orderColumns.Add($"{QuoteIdentifier(isDefaultColumn)} DESC");
        }

        if (schema.TryGetColumn("Order", out var orderColumn))
        {
            orderColumns.Add(QuoteIdentifier(orderColumn));
        }

        var orderByClause = orderColumns.Count == 0 ? string.Empty : $" ORDER BY {string.Join(", ", orderColumns)}";
        return $"SELECT * FROM {QuoteIdentifier(TerminalsTableName)}{orderByClause};";
    }

    private static async Task<ApiTerminal?> FindTerminalByColumnAsync(
        DbConnection connection,
        TerminalTableSchema schema,
        string columnName,
        object value,
        CancellationToken cancellationToken)
    {
        if (!schema.TryGetColumn(columnName, out var actualColumnName))
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT * FROM {QuoteIdentifier(TerminalsTableName)}
WHERE {QuoteIdentifier(actualColumnName)} = $value
LIMIT 1;
""";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "$value";
        parameter.Value = value;
        command.Parameters.Add(parameter);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadTerminal(reader);
    }

    private static async Task<long> CountTerminalsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {QuoteIdentifier(TerminalsTableName)};";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private static async Task InsertTerminalAsync(
        DbConnection connection,
        TerminalTableSchema schema,
        ApiTerminal terminal,
        CancellationToken cancellationToken)
    {
        await using var command = BuildInsertCommand(connection, schema, terminal);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DbCommand BuildInsertCommand(DbConnection connection, TerminalTableSchema schema, ApiTerminal terminal)
    {
        var values = new List<(string ColumnName, object Value)>
        {
            (schema.RequireColumn("Id"), terminal.Id),
            (schema.RequireColumn("Order"), terminal.Order),
            (schema.RequireColumn("Code"), terminal.Code),
            (schema.RequireColumn("Designation"), terminal.Designation),
            (schema.RequireColumn("HardwareId"), terminal.HardwareId)
        };

        if (schema.TryGetColumn("TimerInterval", out var timerIntervalColumn))
        {
            values.Add((timerIntervalColumn, terminal.TimerInterval));
        }

        if (schema.TryGetColumn("IsDefault", out var isDefaultColumn))
        {
            values.Add((isDefaultColumn, terminal.IsDefault));
        }

        if (schema.CreatedTimestampColumn is not null)
        {
            values.Add((schema.CreatedTimestampColumn, terminal.CreatedUtc));
        }

        if (schema.UpdatedTimestampColumn is not null)
        {
            values.Add((schema.UpdatedTimestampColumn, terminal.UpdatedUtc));
        }

        AddKnownSchemaCompatibilityValues(schema, values);

        var command = connection.CreateCommand();
        var parameterNames = new List<string>(values.Count);
        for (var index = 0; index < values.Count; index++)
        {
            var parameterName = $"$p{index}";
            parameterNames.Add(parameterName);

            var parameter = command.CreateParameter();
            parameter.ParameterName = parameterName;
            parameter.Value = values[index].Value;
            command.Parameters.Add(parameter);
        }

        command.CommandText = $"""
INSERT INTO {QuoteIdentifier(TerminalsTableName)} ({string.Join(", ", values.Select(item => QuoteIdentifier(item.ColumnName)))})
VALUES ({string.Join(", ", parameterNames)});
""";

        return command;
    }

    private static void AddKnownSchemaCompatibilityValues(TerminalTableSchema schema, List<(string ColumnName, object Value)> values)
    {
        AddIfExists(schema, values, "CreatedBy", Guid.Empty);
        AddIfExists(schema, values, "UpdatedBy", Guid.Empty);
        AddIfExists(schema, values, "Notes", string.Empty);
        AddIfExists(schema, values, "IsDeleted", false);
    }

    private static void AddIfExists(TerminalTableSchema schema, List<(string ColumnName, object Value)> values, string columnName, object value)
    {
        if (schema.TryGetColumn(columnName, out var actualColumnName))
        {
            values.Add((actualColumnName, value));
        }
    }

    private static async Task<List<ApiTerminal>> ReadTerminalsAsync(DbCommand command, CancellationToken cancellationToken)
    {
        var terminals = new List<ApiTerminal>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            terminals.Add(ReadTerminal(reader));
        }

        return terminals;
    }

    private static ApiTerminal ReadTerminal(DbDataReader reader)
    {
        var columns = GetColumnOrdinals(reader);
        var createdUtc = GetDateTime(reader, columns, "CreatedAt", "CreatedUtc") ?? DateTime.UtcNow;
        var updatedUtc = GetDateTime(reader, columns, "UpdatedAt", "UpdatedUtc") ?? createdUtc;

        return new ApiTerminal
        {
            Id = GetGuid(reader, columns, "Id") ?? Guid.Empty,
            Order = GetUInt32(reader, columns, "Order") ?? 0,
            Code = GetString(reader, columns, "Code") ?? string.Empty,
            Designation = GetString(reader, columns, "Designation") ?? string.Empty,
            HardwareId = GetString(reader, columns, "HardwareId") ?? string.Empty,
            TimerInterval = GetUInt32(reader, columns, "TimerInterval") ?? 100,
            IsDefault = GetBoolean(reader, columns, "IsDefault") ?? false,
            CreatedUtc = createdUtc,
            UpdatedUtc = updatedUtc
        };
    }

    private static Dictionary<string, int> GetColumnOrdinals(DbDataReader reader)
    {
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < reader.FieldCount; index++)
        {
            columns[reader.GetName(index)] = index;
        }

        return columns;
    }

    private static string? GetString(DbDataReader reader, IReadOnlyDictionary<string, int> columns, params string[] candidateNames)
    {
        var ordinal = FindOrdinal(columns, candidateNames);
        if (ordinal is null || reader.IsDBNull(ordinal.Value))
        {
            return null;
        }

        return reader.GetValue(ordinal.Value) switch
        {
            string value => value,
            _ => Convert.ToString(reader.GetValue(ordinal.Value), CultureInfo.InvariantCulture)
        };
    }

    private static Guid? GetGuid(DbDataReader reader, IReadOnlyDictionary<string, int> columns, params string[] candidateNames)
    {
        var ordinal = FindOrdinal(columns, candidateNames);
        if (ordinal is null || reader.IsDBNull(ordinal.Value))
        {
            return null;
        }

        var value = reader.GetValue(ordinal.Value);
        return value switch
        {
            Guid guid => guid,
            byte[] bytes when bytes.Length == 16 => new Guid(bytes),
            string text when Guid.TryParse(text, out var guid) => guid,
            _ => Guid.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var guid) ? guid : null
        };
    }

    private static uint? GetUInt32(DbDataReader reader, IReadOnlyDictionary<string, int> columns, params string[] candidateNames)
    {
        var ordinal = FindOrdinal(columns, candidateNames);
        if (ordinal is null || reader.IsDBNull(ordinal.Value))
        {
            return null;
        }

        var value = reader.GetValue(ordinal.Value);
        return value switch
        {
            byte number => number,
            short number when number >= 0 => (uint)number,
            int number when number >= 0 => (uint)number,
            long number when number >= 0 => (uint)number,
            uint number => number,
            ulong number when number <= uint.MaxValue => (uint)number,
            _ when uint.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    private static bool? GetBoolean(DbDataReader reader, IReadOnlyDictionary<string, int> columns, params string[] candidateNames)
    {
        var ordinal = FindOrdinal(columns, candidateNames);
        if (ordinal is null || reader.IsDBNull(ordinal.Value))
        {
            return null;
        }

        var value = reader.GetValue(ordinal.Value);
        return value switch
        {
            bool boolean => boolean,
            byte number => number != 0,
            short number => number != 0,
            int number => number != 0,
            long number => number != 0,
            string text when bool.TryParse(text, out var parsedBoolean) => parsedBoolean,
            string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedNumber) => parsedNumber != 0,
            _ => null
        };
    }

    private static DateTime? GetDateTime(DbDataReader reader, IReadOnlyDictionary<string, int> columns, params string[] candidateNames)
    {
        var ordinal = FindOrdinal(columns, candidateNames);
        if (ordinal is null || reader.IsDBNull(ordinal.Value))
        {
            return null;
        }

        var value = reader.GetValue(ordinal.Value);
        return value switch
        {
            DateTime dateTime => dateTime,
            DateTimeOffset dateTimeOffset => dateTimeOffset.UtcDateTime,
            string text when DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedDateTime) => parsedDateTime,
            _ => DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedDateTime) ? parsedDateTime : null
        };
    }

    private static int? FindOrdinal(IReadOnlyDictionary<string, int> columns, params string[] candidateNames)
    {
        foreach (var candidateName in candidateNames)
        {
            if (columns.TryGetValue(candidateName, out var ordinal))
            {
                return ordinal;
            }
        }

        return null;
    }

    private static TerminalResponse Map(ApiTerminal terminal)
    {
        return new TerminalResponse
        {
            Id = terminal.Id,
            Notes = string.Empty,
            CreatedAt = terminal.CreatedUtc,
            UpdatedAt = terminal.UpdatedUtc,
            UpdatedBy = Guid.Empty,
            IsDeleted = false,
            Order = terminal.Order,
            Code = terminal.Code,
            Designation = terminal.Designation,
            HardwareId = terminal.HardwareId,
            TimerInterval = terminal.TimerInterval,
            IsDefault = terminal.IsDefault
        };
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private sealed record TerminalColumnInfo(string Name, bool IsRequired, string? DefaultValue, bool IsPrimaryKey);

    private sealed class TerminalTableSchema
    {
        private readonly Dictionary<string, TerminalColumnInfo> _columnsByName;

        public TerminalTableSchema(IEnumerable<TerminalColumnInfo> columns)
        {
            _columnsByName = columns.ToDictionary(column => column.Name, StringComparer.OrdinalIgnoreCase);
            CreatedTimestampColumn = GetFirstMatchingColumn("CreatedAt", "CreatedUtc");
            UpdatedTimestampColumn = GetFirstMatchingColumn("UpdatedAt", "UpdatedUtc");
        }

        public string? CreatedTimestampColumn { get; }

        public string? UpdatedTimestampColumn { get; }

        public bool TryGetColumn(string candidateName, out string actualColumnName)
        {
            if (_columnsByName.TryGetValue(candidateName, out var column))
            {
                actualColumnName = column.Name;
                return true;
            }

            actualColumnName = string.Empty;
            return false;
        }

        public string RequireColumn(string candidateName)
        {
            if (TryGetColumn(candidateName, out var actualColumnName))
            {
                return actualColumnName;
            }

            throw new InvalidOperationException($"The LogicPOS 'Terminals' table is missing required column '{candidateName}'.");
        }

        private string? GetFirstMatchingColumn(params string[] candidateNames)
        {
            foreach (var candidateName in candidateNames)
            {
                if (TryGetColumn(candidateName, out var actualColumnName))
                {
                    return actualColumnName;
                }
            }

            return null;
        }
    }
}
