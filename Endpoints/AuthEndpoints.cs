using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace LogicPOS.ApiServer.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth")
            .WithTags("Login");

        auth.MapPost("/login", () =>
                Results.Json("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.logicpos-login.signature"))
            .WithName("Login");

        auth.MapPost("/sign-in", () =>
                Results.Json("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.logicpos-sign-in.signature"))
            .WithName("SignIn");

        var users = app.MapGroup("/users")
            .WithTags("Login");

        users.MapGet("", async (AppDbContext db, CancellationToken cancellationToken) =>
                Results.Ok(await db.Users.AsNoTracking().ToListAsync(cancellationToken)))
            .WithName("GetUsers");

        users.MapGet("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken cancellationToken) =>
            {
                var user = await db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

                return user is null ? Results.NotFound() : Results.Ok(user);
            })
            .WithName("GetUserById");

        users.MapGet("/{id:guid}/name", async (Guid id, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var rows = await SqliteTableReader.ReadTableAsync(db, "Users", cancellationToken);
            var user = rows.FirstOrDefault(row => GuidEquals(row.GetValueOrDefault("Id"), id));
            return user is null ? Results.NotFound() : Results.Json(Convert.ToString(user.GetValueOrDefault("Name")) ?? string.Empty);
        }).WithName("GetUserNameById");

        users.MapPost("", async (CreateUserRequest request, AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (request.ProfileId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name) ||
                string.IsNullOrWhiteSpace(request.Login) || request.Login.Length < 5)
                return Results.Problem(title: "ProfileId, name and a valid login are required.", statusCode: StatusCodes.Status400BadRequest);

            var id = Guid.NewGuid();
            var now = DateTime.UtcNow.ToString("O");
            var order = await NextOrderAsync(db, cancellationToken);
            var values = UserValues(request, id, order, now);
            var affected = await ExecuteAsync(db, """
                INSERT INTO "Users" ("Id","ProfileId","CommissionGroupId","Residence","Locality","ZipCpde","City","DateOfContract","Phone","MobilePhone","Email","FiscalNumber","Language","AssignedSeating","AccessPin","AccessCardNumber","Login","Password","PasswordReset","PasswordResetDate","BaseConsumption","BaseOffers","PVPOffers","Remarks","ButtonImage","Notes","CreatedAt","CreatedBy","CreatedWhere","UpdatedAt","UpdatedBy","UpdatedWhere","IsDeleted","DeletedAt","Code","Order","Name")
                VALUES (@id,@profileId,@commissionGroupId,@residence,@locality,@zipCpde,@city,@dateOfContract,@phone,@mobilePhone,@email,@fiscalNumber,@language,@assignedSeating,'',NULL,@login,NULL,1,@passwordResetDate,@baseConsumption,@baseOffers,@pvpOffers,@remarks,@buttonImage,@notes,@createdAt,'api','api',@updatedAt,'api','api',0,'',@code,@order,@name)
                """, values, cancellationToken);
            return affected == 0 ? Results.Problem(statusCode: StatusCodes.Status400BadRequest) : Results.Created($"/users/{id}", new { id });
        }).WithName("AddUser");

        users.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (request.ProfileId == Guid.Empty || string.IsNullOrWhiteSpace(request.Code) ||
                string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Login))
                return Results.Problem(title: "Code, profileId, name and login are required.", statusCode: StatusCodes.Status400BadRequest);
            var affected = await ExecuteAsync(db, """
                UPDATE "Users" SET "ProfileId"=@profileId,"CommissionGroupId"=@commissionGroupId,"Residence"=@residence,"Locality"=@locality,"ZipCpde"=@zipCpde,"City"=@city,"DateOfContract"=@dateOfContract,"Phone"=@phone,"MobilePhone"=@mobilePhone,"Email"=@email,"FiscalNumber"=@fiscalNumber,"Language"=@language,"AssignedSeating"=@assignedSeating,"BaseConsumption"=@baseConsumption,"BaseOffers"=@baseOffers,"PVPOffers"=@pvpOffers,"Remarks"=@remarks,"ButtonImage"=@buttonImage,"Notes"=@notes,"UpdatedAt"=@updatedAt,"UpdatedBy"='api',"UpdatedWhere"='api',"IsDeleted"=@isDeleted,"Code"=@code,"Order"=@order,"Name"=@name,"Login"=@login WHERE "Id"=@id
                """, new Dictionary<string, object?>
            {
                ["@id"] = id.ToString(), ["@profileId"] = request.ProfileId.ToString(), ["@commissionGroupId"] = request.CommissionGroupId?.ToString(), ["@residence"] = request.Residence, ["@locality"] = request.Locality, ["@zipCpde"] = request.ZipCpde, ["@city"] = request.City, ["@dateOfContract"] = request.DateOfContract, ["@phone"] = request.Phone, ["@mobilePhone"] = request.MobilePhone, ["@email"] = request.Email, ["@fiscalNumber"] = request.FiscalNumber, ["@language"] = request.Language, ["@assignedSeating"] = request.AssignedSeating, ["@baseConsumption"] = request.BaseConsumption, ["@baseOffers"] = request.BaseOffers, ["@pvpOffers"] = request.PvpOffers, ["@remarks"] = request.Remarks, ["@buttonImage"] = request.ButtonImage, ["@notes"] = request.Notes, ["@updatedAt"] = DateTime.UtcNow.ToString("O"), ["@isDeleted"] = request.IsDeleted ? 1 : 0, ["@code"] = request.Code, ["@order"] = request.Order, ["@name"] = request.Name, ["@login"] = request.Login
            }, cancellationToken);
            return affected == 0 ? Results.NotFound() : Results.Ok();
        }).WithName("UpdateUser");

        users.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var affected = await ExecuteAsync(db, "UPDATE \"Users\" SET \"IsDeleted\"=1,\"DeletedAt\"=@deletedAt,\"UpdatedAt\"=@updatedAt,\"UpdatedBy\"='api',\"UpdatedWhere\"='api' WHERE \"Id\"=@id AND \"IsDeleted\"=0", new Dictionary<string, object?> { ["@id"] = id.ToString(), ["@deletedAt"] = DateTime.UtcNow.ToString("O"), ["@updatedAt"] = DateTime.UtcNow.ToString("O") }, cancellationToken);
            return affected == 0 ? Results.NotFound() : Results.Ok();
        }).WithName("DeleteUser");

        users.MapPut("/{userId:guid}/reset-password", async (Guid userId, ResetPasswordRequest request, AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword)) return Results.Problem(title: "New password is required.", statusCode: StatusCodes.Status400BadRequest);
            var rows = await SqliteTableReader.ReadTableAsync(db, "Users", cancellationToken);
            var user = rows.FirstOrDefault(row => GuidEquals(row.GetValueOrDefault("Id"), userId));
            if (user is null) return Results.NotFound();
            var currentPassword = Convert.ToString(user.GetValueOrDefault("Password"));
            var oldPasswordHash = string.IsNullOrWhiteSpace(request.OldPassword)
                ? string.Empty
                : HashPassword(request.OldPassword);
            if (!string.IsNullOrWhiteSpace(currentPassword) && !string.Equals(currentPassword, oldPasswordHash, StringComparison.OrdinalIgnoreCase))
                return Results.Problem(title: "The current password is invalid.", statusCode: StatusCodes.Status400BadRequest);
            var affected = await ExecuteAsync(db, "UPDATE \"Users\" SET \"Password\"=@password,\"PasswordReset\"=0,\"PasswordResetDate\"=@resetDate,\"UpdatedAt\"=@updatedAt,\"UpdatedBy\"='api',\"UpdatedWhere\"='api' WHERE \"Id\"=@id", new Dictionary<string, object?> { ["@password"] = HashPassword(request.NewPassword), ["@resetDate"] = DateTime.UtcNow.ToString("O"), ["@updatedAt"] = DateTime.UtcNow.ToString("O"), ["@id"] = userId.ToString() }, cancellationToken);
            return affected == 0 ? Results.NotFound() : Results.Ok();
        }).WithName("ResetPassword");

        users.MapGet("/{id:guid}/permissions", async (Guid id, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var permissions = new List<string>();

            try
            {
                var userRows = await SqliteTableReader.ReadTableAsync(db, "Users", cancellationToken);
                var user = userRows.FirstOrDefault(u => u.TryGetValue("Id", out var uid) &&
                    Guid.TryParse(Convert.ToString(uid), out var parsedId) && parsedId == id);

                if (user != null && user.TryGetValue("ProfileId", out var profVal) &&
                    Guid.TryParse(Convert.ToString(profVal), out var profileId))
                {
                    var profRows = await SqliteTableReader.ReadTableAsync(db, "PermissionProfiles", cancellationToken);
                    var itemRows = await SqliteTableReader.ReadTableAsync(db, "PermissionItems", cancellationToken);

                    var grantedItemIds = profRows
                        .Where(p => p.TryGetValue("UserProfileId", out var upId) &&
                                    Guid.TryParse(Convert.ToString(upId), out var pProfileId) && pProfileId == profileId &&
                                    (Convert.ToString(p.GetValueOrDefault("Granted")) == "1" ||
                                     string.Equals(Convert.ToString(p.GetValueOrDefault("Granted")), "true", StringComparison.OrdinalIgnoreCase)))
                        .Select(p => Guid.TryParse(Convert.ToString(p.GetValueOrDefault("PermissionItemId")), out var piId) ? piId : Guid.Empty)
                        .Where(piId => piId != Guid.Empty)
                        .ToHashSet();

                    if (grantedItemIds.Count > 0)
                    {
                        permissions.AddRange(itemRows
                            .Where(i => i.TryGetValue("Id", out var itemId) &&
                                        Guid.TryParse(Convert.ToString(itemId), out var pItemId) && grantedItemIds.Contains(pItemId))
                            .Select(i => Convert.ToString(i.GetValueOrDefault("Token")))
                            .Where(token => !string.IsNullOrWhiteSpace(token))!);
                    }
                }
            }
            catch
            {
                // Fallback to all permissions
            }

            if (permissions.Count == 0)
            {
                try
                {
                    var itemRows = await SqliteTableReader.ReadTableAsync(db, "PermissionItems", cancellationToken);
                    permissions.AddRange(itemRows
                        .Select(i => Convert.ToString(i.GetValueOrDefault("Token")))
                        .Where(token => !string.IsNullOrWhiteSpace(token))!);
                }
                catch
                {
                    // Fallback
                }
            }

            return Results.Ok(permissions);
        })
        .WithName("GetUserPermissions");
    }

    private static bool GuidEquals(object? value, Guid target) => Guid.TryParse(Convert.ToString(value), out var parsed) && parsed == target;

    private static string HashPassword(string password) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));

    private static async Task<int> NextOrderAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var rows = await SqliteTableReader.ReadTableAsync(db, "Users", cancellationToken);
        return rows.Count == 0 ? 0 : rows.Max(row => Convert.ToInt32(row.GetValueOrDefault("Order") ?? -1)) + 1;
    }

    private static Dictionary<string, object?> UserValues(CreateUserRequest request, Guid id, int order, string now) => new()
    {
        ["@id"] = id.ToString(), ["@profileId"] = request.ProfileId.ToString(), ["@commissionGroupId"] = request.CommissionGroupId?.ToString(), ["@residence"] = request.Residence, ["@locality"] = request.Locality, ["@zipCpde"] = request.ZipCode, ["@city"] = request.City, ["@dateOfContract"] = request.DateOfContract, ["@phone"] = request.Phone, ["@mobilePhone"] = request.MobilePhone, ["@email"] = request.Email, ["@fiscalNumber"] = request.FiscalNumber, ["@language"] = request.Language, ["@assignedSeating"] = request.AssignedSeating, ["@login"] = request.Login, ["@passwordResetDate"] = now, ["@baseConsumption"] = request.BaseConsumption, ["@baseOffers"] = request.BaseOffers, ["@pvpOffers"] = request.PvpOffers, ["@remarks"] = request.Remarks, ["@buttonImage"] = request.ButtonImage, ["@notes"] = request.Notes, ["@createdAt"] = now, ["@updatedAt"] = now, ["@code"] = request.Login, ["@order"] = order, ["@name"] = request.Name
    };

    private static async Task<int> ExecuteAsync(AppDbContext db, string sql, Dictionary<string, object?> values, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection(); var shouldClose = connection.State == System.Data.ConnectionState.Closed;
        if (shouldClose) await connection.OpenAsync(cancellationToken);
        try { await using var command = connection.CreateCommand(); command.CommandText = sql; foreach (var pair in values) { var parameter = command.CreateParameter(); parameter.ParameterName = pair.Key; parameter.Value = pair.Value ?? DBNull.Value; command.Parameters.Add(parameter); } return await command.ExecuteNonQueryAsync(cancellationToken); }
        finally { if (shouldClose) await connection.CloseAsync(); }
    }
}

public class CreateUserRequest
{
    public Guid ProfileId { get; set; }
    public Guid? CommissionGroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string? Residence { get; set; }
    public string? Locality { get; set; }
    public string? ZipCode { get; set; }
    public string? City { get; set; }
    public string? DateOfContract { get; set; }
    public string? Phone { get; set; }
    public string? MobilePhone { get; set; }
    public string? Email { get; set; }
    public string? FiscalNumber { get; set; }
    public string? Language { get; set; }
    public string? AssignedSeating { get; set; }
    public string? BaseConsumption { get; set; }
    public string? BaseOffers { get; set; }
    public string? PvpOffers { get; set; }
    public string? Remarks { get; set; }
    public string? ButtonImage { get; set; }
    public string? Notes { get; set; }
}

public sealed class UpdateUserRequest : CreateUserRequest
{
    public int Order { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? ZipCpde { get; set; }
    public bool IsDeleted { get; set; }
}

public sealed class ResetPasswordRequest
{
    public string? OldPassword { get; set; }
    public string NewPassword { get; set; } = string.Empty;
}
