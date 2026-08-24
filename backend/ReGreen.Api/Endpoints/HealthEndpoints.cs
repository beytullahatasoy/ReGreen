using Microsoft.EntityFrameworkCore;
using ReGreen.Api.Dtos;
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
        app.MapGet("/health/live", () => Results.Ok(HealthResponseDto.Healthy))
            .Produces<HealthResponseDto>();

        app.MapGet("/health/ready", async (AppDbContext db, CancellationToken ct) =>
            {
                var canConnect = await db.Database.CanConnectAsync(ct);
                return canConnect
                    ? Results.Ok(HealthResponseDto.Healthy)
                    : Results.Json(HealthResponseDto.Unhealthy, statusCode: StatusCodes.Status503ServiceUnavailable);
            })
            .Produces<HealthResponseDto>()
            .Produces<HealthResponseDto>(StatusCodes.Status503ServiceUnavailable);
    }
}
