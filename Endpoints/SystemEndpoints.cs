using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LogicPOS.ApiServer.Endpoints;

public static class SystemEndpoints
{
    public static void MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        var system = app.MapGroup("/system")
            .WithTags("System");

        system.MapGet("/api-version", () => Results.Text("1.5.2 retail", "text/plain"))
            .WithName("GetApiVersion");

        system.MapGet("/information", () => Results.Ok(new
        {
            Culture = "pt-PT",
            Country = "pt",
            DatabaseModule = "SQLite"
        }))
        .WithName("GetSystemInformation");

        var preferenceParameters = app.MapGroup("/preference-parameters")
            .WithTags("Preference Parameters");

        preferenceParameters.MapGet("", () => Results.Ok(Array.Empty<object>()))
            .WithName("GetPreferenceParameters");

        var licensing = app.MapGroup("/licensing")
            .WithTags("System");

        licensing.MapGet("/data", () => Results.Ok(new
        {
            status = "Active"
        }))
        .WithName("GetLicensingData");

        licensing.MapGet("/hardware-id", () => Results.Ok(new
        {
            hardwareId = "LINUX-LOCAL"
        }))
        .WithName("GetLicensingHardwareId");
    }
}
