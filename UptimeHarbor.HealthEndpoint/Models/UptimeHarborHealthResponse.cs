using System.Text.Json.Serialization;

namespace UptimeHarbor.HealthEndpoint.Models;

internal class UptimeHarborHealthResponse(string name, DateTimeOffset startedAtUtc)
{
    [JsonPropertyName("name")]
    public string Name { get; } = name;

    [JsonPropertyName("startedAtUtc")]
    public DateTimeOffset StartedAtUtc { get; } = startedAtUtc;
}
