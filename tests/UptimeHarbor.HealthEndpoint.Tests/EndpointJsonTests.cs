using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
using MvcJsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

namespace UptimeHarbor.HealthEndpoint.Tests;

public class EndpointJsonTests
{
    [Fact]
    public async Task Get_WithCustomGlobalJson_IsIsolatedAndDoesNotChangeOtherResponses()
    {
        // Arrange
        using EndpointTestPipeline pipeline = new(configureServices: services =>
        {
            services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper;
                options.SerializerOptions.Converters.Add(new ApplicationTimeConverter());
                options.SerializerOptions.WriteIndented = true;
            });
            services.Configure<MvcJsonOptions>(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
                options.JsonSerializerOptions.Converters.Add(new ApplicationTimeConverter());
            });
        }, map: endpoints =>
        {
            endpoints.MapUptimeHarborHealth();
            endpoints.MapGet("/other", () => Results.Json(new
            {
                DisplayName = "ordinary",
                StartedAtUtc = DateTimeOffset.UnixEpoch
            })).AllowAnonymous();
        });
        HttpJsonOptions httpOptions = pipeline.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value;
        MvcJsonOptions mvcOptions = pipeline.Services.GetRequiredService<IOptions<MvcJsonOptions>>().Value;
        object ordinary = new { DisplayName = "ordinary", StartedAtUtc = DateTimeOffset.UnixEpoch };
        string mvcBefore = JsonSerializer.Serialize(ordinary, mvcOptions.JsonSerializerOptions);

        // Act
        DefaultHttpContext health = await pipeline.GetAsync();
        DefaultHttpContext other = await pipeline.GetAsync("/other");
        string mvcAfter = JsonSerializer.Serialize(ordinary, mvcOptions.JsonSerializerOptions);
        using JsonDocument json = JsonDocument.Parse(EndpointTestPipeline.Body(health));
        using JsonDocument otherJson = JsonDocument.Parse(EndpointTestPipeline.Body(other));

        // Assert
        Assert.Equal(200, health.Response.StatusCode);
        Assert.Equal(new[] { "name", "startedAtUtc" }, json.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("orders-api", json.RootElement.GetProperty("name").GetString());
        Assert.Equal(pipeline.Services.GetRequiredService<UptimeHarborHealthState>().StartedAtUtc,
            DateTimeOffset.Parse(json.RootElement.GetProperty("startedAtUtc").GetString()!, CultureInfo.InvariantCulture));
        Assert.Equal("application-time", otherJson.RootElement.GetProperty("STARTED_AT_UTC").GetString());
        Assert.Equal("ordinary", otherJson.RootElement.GetProperty("DISPLAY_NAME").GetString());
        Assert.Same(JsonNamingPolicy.SnakeCaseUpper, httpOptions.SerializerOptions.PropertyNamingPolicy);
        Assert.Single(httpOptions.SerializerOptions.Converters);
        Assert.Equal(mvcBefore, mvcAfter);
        Assert.Contains("\"started_at_utc\":\"application-time\"", mvcAfter);
        Assert.Single(mvcOptions.JsonSerializerOptions.Converters);
    }
}
