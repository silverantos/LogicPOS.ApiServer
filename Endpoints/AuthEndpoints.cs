using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

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
    }
}
