using Microsoft.EntityFrameworkCore;
using ReGreen.Data;

namespace ReGreen.Api.Endpoints;

/// <summary>
/// Ops/monitoring için standart liveness/readiness ayrımı. `/health/live` süreç ayakta mı
/// diye bakar (DB'ye hiç dokunmaz — DB çökse bile uygulama restart edilmemeli).
/// `/health/ready` gerçekten trafik almaya hazır mı diye DB bağlantısını da kontrol eder.
/// </summary>
public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }))
            .Produces<object>();

        app.MapGet("/health/ready", async (AppDbContext db, CancellationToken ct) =>
            {
                var canConnect = await db.Database.CanConnectAsync(ct);
                return canConnect
                    ? Results.Ok(new { status = "healthy" })
                    : Results.Json(new { status = "unhealthy" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            })
            .Produces<object>()
            .Produces<object>(StatusCodes.Status503ServiceUnavailable);
    }
}
