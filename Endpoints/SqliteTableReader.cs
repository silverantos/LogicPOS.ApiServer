using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Globalization;

namespace LogicPOS.ApiServer.Endpoints;

internal static class SqliteTableReader
{
    internal static async Task<List<Dictionary<string, object?>>> ReadTableAsync(
        AppDbContext db,
        string tableName,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State == System.Data.ConnectionState.Closed;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM {QuoteIdentifier(tableName)}";

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await ReadRowsAsync(reader, cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    internal static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }

    private static async Task<List<Dictionary<string, object?>>> ReadRowsAsync(
        DbDataReader reader,
        CancellationToken cancellationToken)
    {
        var rows = new List<Dictionary<string, object?>>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                var value = await reader.IsDBNullAsync(i, cancellationToken)
                    ? null
                    : reader.GetValue(i);

                row[name] = NormalizeSqliteValue(name, value);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static object? NormalizeSqliteValue(string columnName, object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            if (TryConvertSqliteDateTime(text, out var convertedDateTime))
            {
                return convertedDateTime;
            }

            if (LooksLikeBooleanColumn(columnName) && (text == "0" || text == "1"))
            {
                return text == "1";
            }

            return text;
        }

        if (LooksLikeBooleanColumn(columnName) && value is byte or sbyte or short or ushort or int or uint or long or ulong)
        {
            var numericValue = Convert.ToInt64(value);
            return numericValue == 1;
        }

        return value;
    }

    private static bool TryConvertSqliteDateTime(string value, out DateTime dateTime)
    {
        dateTime = default;

        if (!LooksLikeSqliteDateTime(value))
        {
            return false;
        }

        var formats = new[]
        {
            "yyyy-MM-dd HH:mm:ss.fffffff",
            "yyyy-MM-dd HH:mm:ss.ffffff",
            "yyyy-MM-dd HH:mm:ss.fffff",
            "yyyy-MM-dd HH:mm:ss.ffff",
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-dd HH:mm:ss.ff",
            "yyyy-MM-dd HH:mm:ss.f",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.fffffff",
            "yyyy-MM-ddTHH:mm:ss.ffffff",
            "yyyy-MM-ddTHH:mm:ss.fffff",
            "yyyy-MM-ddTHH:mm:ss.ffff",
            "yyyy-MM-ddTHH:mm:ss.fff",
            "yyyy-MM-ddTHH:mm:ss.ff",
            "yyyy-MM-ddTHH:mm:ss.f",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-dd",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-ddTHH:mm"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
            {
                dateTime = parsed;
                return true;
            }
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsedDate))
        {
            dateTime = parsedDate;
            return true;
        }

        return false;
    }

    private static bool LooksLikeSqliteDateTime(string value)
    {
        if (value.Length < 8)
        {
            return false;
        }

        var trimmed = value.Trim();

        if (trimmed.Contains("/"))
        {
            return false;
        }

        return trimmed.Contains('-') && (trimmed.Contains(':') || trimmed.Length == 10 || trimmed.Length == 8);
    }

    private static readonly HashSet<string> KnownBooleanColumnNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "AgtFeModule",
        "AllowPayback",
        "AtResendDocument",
        "Connected",
        "Credit",
        "Delete",
        "DryRun",
        "Favorite",
        "Fixed",
        "ForceAtCommunication",
        "Granted",
        "HasActiveSeries",
        "HasExpired",
        "HasExternalDocument",
        "HasLocation",
        "HasPrice",
        "HasSerialNumber",
        "HasValidationCode",
        "Hidden",
        "IncludePrintingModel",
        "IsComposed",
        "IsDefault",
        "IsDeleted",
        "IsDraft",
        "IsFailure",
        "IsLicensed",
        "IsPortugal",
        "IsRead",
        "IsSdrPackaging",
        "IsSuccess",
        "IsThermalPrint",
        "IsValid",
        "IsWayBill",
        "NewPaid",
        "Paid",
        "PasswordReset",
        "PreviousPaid",
        "Price1UsePromotion",
        "Price2UsePromotion",
        "Price3UsePromotion",
        "Price4UsePromotion",
        "Price5UsePromotion",
        "Price1_UsePromotion",
        "Price2_UsePromotion",
        "Price3_UsePromotion",
        "Price4_UsePromotion",
        "Price5_UsePromotion",
        "PriceWithVat",
        "PrintOpenDrawer",
        "PrintRequestConfirmation",
        "PrintRequestMotive",
        "PVPVariable",
        "Required",
        "SaftAuditFile",
        "SecondPrint",
        "SendReceipts",
        "SeriesForEachTerminal",
        "ShowInDialog",
        "StocksModule",
        "Success",
        "Supplier",
        "ThermalPrintLogo",
        "ThermalPrinter",
        "TypeOpenDrawer",
        "UniqueArticle",
        "UniqueArticles",
        "UsePromotion",
        "UseWeighingBalance",
        "VatDirectSelling",
        "WayBill",
        "WorkInStock",
        "WsAtDocument"
    };

    private static bool LooksLikeBooleanColumn(string columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName))
        {
            return false;
        }

        var normalizedName = columnName.Trim();
        if (KnownBooleanColumnNames.Contains(normalizedName))
        {
            return true;
        }

        return normalizedName.StartsWith("Is", StringComparison.OrdinalIgnoreCase)
            || normalizedName.StartsWith("Has", StringComparison.OrdinalIgnoreCase)
            || normalizedName.EndsWith("Enabled", StringComparison.OrdinalIgnoreCase)
            || normalizedName.EndsWith("Active", StringComparison.OrdinalIgnoreCase)
            || normalizedName.EndsWith("Deleted", StringComparison.OrdinalIgnoreCase)
            || normalizedName.EndsWith("Default", StringComparison.OrdinalIgnoreCase)
            || normalizedName.Contains("Is", StringComparison.OrdinalIgnoreCase);
    }
}
