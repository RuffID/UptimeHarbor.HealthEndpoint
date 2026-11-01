using System.Text.Json;
using System.Text.Json.Serialization;

namespace UptimeHarbor.HealthEndpoint.Tests;

internal class ApplicationTimeConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException();
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteStringValue("application-time");
    }
}
