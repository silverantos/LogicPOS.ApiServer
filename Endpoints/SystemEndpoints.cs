using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace LogicPOS.ApiServer.Endpoints;

public static class SystemEndpoints
{
    private const string LicenseFileName = "logicpos.license";
    private const string HardwareIdFileName = "logicpos.hardware";
    private const string CurrentVersion = "1.5.2";

    public static void MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        var system = app.MapGroup("/system").WithTags("System");

        system.MapGet("/api-version", () => Results.Ok(CurrentVersion))
            .WithName("GetApiVersion");

        system.MapGet("/information", () => Results.Ok(new
        {
            Culture = "pt-PT",
            CountryCode2 = "pt",
            Module = "default"
        }))
        .WithName("GetSystemInformation");

        system.MapGet("/system-notifications", () => Results.Ok(Array.Empty<object>()))
            .WithName("GetSystemNotifications");

        var preferenceParameters = app.MapGroup("/preference-parameters")
            .WithTags("Preference Parameters");

        preferenceParameters.MapGet("", async (AppDbContext db, CancellationToken cancellationToken) =>
                Results.Ok(await SqliteTableReader.ReadTableAsync(db, "PreferenceParameters", cancellationToken)))
            .WithName("GetPreferenceParameters");

        preferenceParameters.MapPut("/{id:guid}", async (Guid id, UpdatePreferenceParameterRequest request,
            AppDbContext db, CancellationToken cancellationToken) =>
        {
            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State == System.Data.ConnectionState.Closed;
            if (shouldClose) await connection.OpenAsync(cancellationToken);

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    UPDATE "PreferenceParameters"
                    SET "Value" = COALESCE(@value, "Value"),
                        "Notes" = COALESCE(@notes, "Notes"),
                        "Code" = COALESCE(@code, "Code"),
                        "Order" = COALESCE(@order, "Order"),
                        "UpdatedAt" = @updatedAt,
                        "UpdatedBy" = @updatedBy,
                        "UpdatedWhere" = @updatedWhere
                    WHERE "Id" = @id AND "IsDeleted" = 0
                    """;
                AddParameter(command, "@id", id.ToString());
                AddParameter(command, "@value", request.Value);
                AddParameter(command, "@notes", request.Notes);
                AddParameter(command, "@code", request.Code);
                AddParameter(command, "@order", request.Order);
                AddParameter(command, "@updatedAt", DateTime.UtcNow.ToString("O"));
                AddParameter(command, "@updatedBy", "api");
                AddParameter(command, "@updatedWhere", "api");

                return await command.ExecuteNonQueryAsync(cancellationToken) == 0
                    ? Results.NotFound()
                    : Results.Ok();
            }
            finally
            {
                if (shouldClose) await connection.CloseAsync();
            }
        })
        .WithName("UpdatePreferenceParameter");

        var licensing = app.MapGroup("/licensing").WithTags("Licensing");

        licensing.MapGet("/data", (IHostEnvironment environment) =>
                Results.Ok(new { data = CreateLicenseData(environment.ContentRootPath) }))
            .WithName("GetLicensingData");

        licensing.MapGet("/hardware-id", (IHostEnvironment environment) => Results.Ok(new
        {
            hardwareId = GetHardwareId(environment.ContentRootPath)
        }))
        .WithName("GetLicensingHardwareId");

        licensing.MapGet("/system/lastest-version", (IHostEnvironment environment) =>
                Results.Ok(CreateVersionResponse(environment.ContentRootPath)))
            .WithName("GetLicensingLastestVersion");

        licensing.MapGet("/system/latest-version", (IHostEnvironment environment) =>
                Results.Ok(CreateVersionResponse(environment.ContentRootPath)))
            .WithName("GetLicensingLatestVersion");

        licensing.MapGet("/connect", () => Results.Ok(new { connected = true }))
            .WithName("ConnectLicensingService");

        licensing.MapPost("/add-message", async (AddLicenseMessageRequest request, AppDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.HardwareId) || string.IsNullOrWhiteSpace(request.Name) ||
                string.IsNullOrWhiteSpace(request.Contact) || string.IsNullOrWhiteSpace(request.Message) ||
                string.IsNullOrWhiteSpace(request.Client))
                return Results.Problem(title: "All message fields are required.", statusCode: StatusCodes.Status400BadRequest);

            var typeRows = await TryReadTableAsync(db, "SystemNotificationTypes", cancellationToken);
            var typeId = typeRows.FirstOrDefault()?.GetValueOrDefault("Id");
            if (typeId is null) return Results.Problem(title: "No notification type is configured.", statusCode: StatusCodes.Status400BadRequest);

            var id = Guid.NewGuid();
            var now = DateTime.UtcNow.ToString("O");
            await ExecuteAsync(db, """
                INSERT INTO "SystemNotifications" ("Id","CreatedAt","CreatedBy","CreatedWhere","DeletedAt","IsDeleted","IsRead","Message","Notes","Ord","ReadingDate","TypeId","UpdatedAt","UpdatedBy","UpdatedWhere")
                VALUES (@id,@createdAt,'api',@hardwareId,'',0,0,@message,@notes,0,'0001-01-01 00:00:00',@typeId,@updatedAt,'api',@client)
                """, new Dictionary<string, object?>
            {
                ["@id"] = id.ToString(), ["@createdAt"] = now, ["@hardwareId"] = request.HardwareId,
                ["@message"] = request.Message, ["@notes"] = $"{request.Name} ({request.Contact})",
                ["@typeId"] = typeId, ["@updatedAt"] = now, ["@client"] = request.Client
            }, cancellationToken);
            return Results.Ok();
        }).WithName("AddLicenseMessage");

        licensing.MapPost("/activate", async (ActivateLicenseRequest request, IHostEnvironment environment,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Company) ||
                string.IsNullOrWhiteSpace(request.Address) || string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.HardwareId) ||
                string.IsNullOrWhiteSpace(request.AssemblyVersion))
                return Results.Problem(title: "License activation fields are required.", statusCode: StatusCodes.Status400BadRequest);

            var payload = System.Text.Json.JsonSerializer.Serialize(request);
            var licensePath = Path.Combine(environment.ContentRootPath, LicenseFileName);
            await File.WriteAllTextAsync(licensePath, Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload)), cancellationToken);
            return Results.Ok(new { activated = true, hardwareId = request.HardwareId, version = request.AssemblyVersion });
        }).WithName("ActivateLicense");

        licensing.MapGet("/countries", () => Results.Ok(new[]
        {
            "PT", "ES", "FR", "DE", "IT", "GB", "BR", "US", "NL", "BE"
        }))
        .WithName("GetLicensingCountries");

        // Refresh must return HTTP 200 because the desktop client treats non-2xx as an API error.
        licensing.MapGet("/refresh", (IHostEnvironment environment) =>
        {
            var validation = ValidateLicenseFile(environment.ContentRootPath);
            return Results.Ok(new { success = validation.Readable, valid = validation.Readable });
        })
        .WithName("RefreshLicense");

        licensing.MapGet("/system/validate", (IHostEnvironment environment) =>
                Results.Ok(CreateValidationResponse(environment.ContentRootPath)))
            .WithName("ValidateLicense");
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static async Task<List<Dictionary<string, object?>>> TryReadTableAsync(AppDbContext db, string tableName, CancellationToken cancellationToken)
    {
        try { return await SqliteTableReader.ReadTableAsync(db, tableName, cancellationToken); }
        catch (InvalidOperationException) { return new List<Dictionary<string, object?>>(); }
    }

    private static async Task<int> ExecuteAsync(AppDbContext db, string sql, Dictionary<string, object?> values, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State == System.Data.ConnectionState.Closed;
        if (shouldClose) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand(); command.CommandText = sql;
            foreach (var pair in values) { var parameter = command.CreateParameter(); parameter.ParameterName = pair.Key; parameter.Value = pair.Value ?? DBNull.Value; command.Parameters.Add(parameter); }
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally { if (shouldClose) await connection.CloseAsync(); }
    }

    private static object CreateVersionResponse(string rootPath)
    {
        var validation = ValidateLicenseFile(rootPath);
        return new { version = validation.Readable ? CurrentVersion : "0.0.0" };
    }

    private static object CreateLicenseData(string rootPath)
    {
        var validation = ValidateLicenseFile(rootPath);
        var licensed = validation.Readable;

        return new
        {
            isLicensed = licensed,
            version = licensed ? CurrentVersion : "0.0.0",
            hardwareId = GetHardwareId(rootPath),
            status = licensed ? 1 : 0,
            date = (DateTime?)null,
            name = (string?)null,
            company = (string?)null,
            nif = (string?)null,
            address = (string?)null,
            email = (string?)null,
            phone = (string?)null,
            reseller = (string?)null,
            stocksModule = false,
            agtFeModule = false,
            allUpdateExpirationDate = (DateTime?)null,
            allNumberOfDevices = (int?)null,
            hasExpired = false,
            isValid = licensed
        };
    }

    private static object CreateValidationResponse(string rootPath)
    {
        var validation = ValidateLicenseFile(rootPath);
        return new
        {
            valid = validation.Readable,
            file = Path.Combine(rootPath, LicenseFileName),
            fileExists = validation.Exists,
            status = validation.Readable ? "Active" : "Inactive",
            validationMode = "base64-structure",
            error = validation.Error
        };
    }

    private static LicenseValidationResult ValidateLicenseFile(string rootPath)
    {
        var licensePath = Path.Combine(rootPath, LicenseFileName);

        try
        {
            if (!File.Exists(licensePath))
                return new(false, false, "The logicpos.license file was not found in the application root.");

            var lines = File.ReadAllLines(licensePath)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line))
                .ToArray();

            if (lines.Length == 0)
                return new(true, false, "The logicpos.license file is empty.");

            foreach (var line in lines)
            {
                try
                {
                    if (Convert.FromBase64String(line).Length == 0)
                        return new(true, false, "The license contains an empty Base64 record.");
                }
                catch (FormatException)
                {
                    return new(true, false, "The license contains an invalid Base64 record.");
                }
            }

            return new(true, true, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(File.Exists(licensePath), false, $"The license file could not be read: {exception.Message}");
        }
    }

    private static string GetHardwareId(string rootPath)
    {
        var hardwarePath = Path.Combine(rootPath, HardwareIdFileName);

        try
        {
            if (File.Exists(hardwarePath))
            {
                var existing = File.ReadAllText(hardwarePath).Trim();
                if (!string.IsNullOrWhiteSpace(existing))
                    return existing;
            }

            var hardwareId = FormatHardwareId(Convert.ToHexString(RandomNumberGenerator.GetBytes(12)));
            File.WriteAllText(hardwarePath, hardwareId);
            return hardwareId;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Environment.MachineName;
        }
    }

    private static string FormatHardwareId(string value)
    {
        return string.Join("-", Enumerable.Range(0, value.Length / 4)
            .Select(index => value.Substring(index * 4, 4)));
    }

    private sealed record LicenseValidationResult(bool Exists, bool Readable, string? Error);
}

public sealed class UpdatePreferenceParameterRequest
{
    public int? Order { get; set; }
    public string? Code { get; set; }
    public string? Value { get; set; }
    public string? Notes { get; set; }
}

public sealed class AddLicenseMessageRequest
{
    public string HardwareId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Client { get; set; } = string.Empty;
}

public sealed class ActivateLicenseRequest
{
    public string Name { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string FiscalNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string HardwareId { get; set; } = string.Empty;
    public string AssemblyVersion { get; set; } = string.Empty;
    public int IdCountry { get; set; }
    public string? SoftwareKey { get; set; }
}
