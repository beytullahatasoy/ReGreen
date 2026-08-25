using System.Text.Json.Serialization;

namespace ReGreen.Api.Dtos;

public record class HealthResponseDto(
    [property: JsonPropertyName("status")] string Status)
{
    public static readonly HealthResponseDto Healthy = new("healthy");
    public static readonly HealthResponseDto Unhealthy = new("unhealthy");
}
