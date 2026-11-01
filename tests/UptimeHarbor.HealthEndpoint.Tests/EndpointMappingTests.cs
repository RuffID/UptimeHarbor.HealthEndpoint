using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace UptimeHarbor.HealthEndpoint.Tests;

public class EndpointMappingTests
{
    [Fact]
    public async Task Get_WithEscapedPathAndApplicationPathBase_UsesDecodedRoute()
    {
        // Arrange
        using EndpointTestPipeline pipeline = new(options => options.Path = "/health%2Dlive", pathBase: "/gateway");

        // Act
        DefaultHttpContext context = await pipeline.GetAsync("/gateway/health-live");

        // Assert
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", context.Response.ContentType);
        Assert.Equal("/health%2Dlive", pipeline.Services.GetRequiredService<UptimeHarborHealthState>().Path);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/monitoring/health")]
    [InlineData("/мониторинг/статус")]
    [InlineData("/Health/")]
    [InlineData("/")]
    public async Task Get_WithFallbackAuthorization_ReturnsExactAnonymousContract(string path)
    {
        // Arrange
        using EndpointTestPipeline pipeline = new(options => options.Path = path);

        // Act
        DefaultHttpContext context = await pipeline.GetAsync(path);
        using JsonDocument json = JsonDocument.Parse(EndpointTestPipeline.Body(context));

        // Assert
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", context.Response.ContentType);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
        Assert.Equal(new[] { "name", "startedAtUtc" }, json.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("orders-api", json.RootElement.GetProperty("name").GetString());
        string time = json.RootElement.GetProperty("startedAtUtc").GetString()!;
        Assert.EndsWith("+00:00", time);
        Assert.Equal(pipeline.Services.GetRequiredService<UptimeHarborHealthState>().StartedAtUtc,
            DateTimeOffset.Parse(time, CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Get_AtOtherPath_StillEnforcesFallbackAuthorization()
    {
        // Arrange
        using EndpointTestPipeline pipeline = new(map: endpoints =>
        {
            endpoints.MapUptimeHarborHealth();
            endpoints.MapGet("/private", () => "private");
        });

        // Act
        DefaultHttpContext context = await pipeline.GetAsync("/private");

        // Assert
        Assert.Equal(401, context.Response.StatusCode);
        Assert.DoesNotContain("private", EndpointTestPipeline.Body(context));
    }

    [Fact]
    public void Map_WithoutAdd_FailsClearlyWithoutRegisteringServices()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddRouting();
        using ServiceProvider provider = services.BuildServiceProvider();
        ApplicationBuilder app = new(provider);
        app.UseRouting();
        int originalCount = services.Count;

        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
        {
            app.UseEndpoints(endpoints => endpoints.MapUptimeHarborHealth());
        });

        // Assert
        Assert.Equal("Call AddUptimeHarborHealth before MapUptimeHarborHealth.", exception.Message);
        Assert.Equal(originalCount, services.Count);
        Assert.Null(provider.GetService<UptimeHarborHealthState>());
    }

    [Fact]
    public void Map_WithNullBuilder_RejectsArgument()
    {
        // Arrange
        IEndpointRouteBuilder endpoints = null!;

        // Act
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => endpoints.MapUptimeHarborHealth());

        // Assert
        Assert.Equal("endpoints", exception.ParamName);
    }

    [Fact]
    public async Task Get_AfterOptionsMutation_UsesOriginalSnapshotAndPath()
    {
        // Arrange
        using EndpointTestPipeline pipeline = new();
        DefaultHttpContext first = await pipeline.GetAsync();
        UptimeHarborHealthOptions options = pipeline.Services.GetRequiredService<IOptions<UptimeHarborHealthOptions>>().Value;

        // Act
        options.Name = "secret-configuration-value";
        options.Path = "/changed";
        DefaultHttpContext second = await pipeline.GetAsync();

        // Assert
        Assert.Equal(200, second.Response.StatusCode);
        Assert.Equal(EndpointTestPipeline.Body(first), EndpointTestPipeline.Body(second));
        Assert.DoesNotContain("secret-configuration-value", EndpointTestPipeline.Body(second));
        Assert.Equal("/health", pipeline.Services.GetRequiredService<UptimeHarborHealthState>().Path);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("HEAD")]
    [InlineData("PUT")]
    public async Task OtherMethods_DoNotExecuteHealthHandler(string method)
    {
        // Arrange
        using EndpointTestPipeline pipeline = new(configureServices: services =>
        {
            services.AddAuthorization(options => options.FallbackPolicy = null);
        });

        // Act
        DefaultHttpContext context = await pipeline.GetAsync(method: method);

        // Assert
        Assert.Equal(405, context.Response.StatusCode);
        Assert.Equal("GET", context.Response.Headers.Allow);
        Assert.Equal(string.Empty, EndpointTestPipeline.Body(context));
    }

    [Fact]
    public async Task Map_InAuthorizedGroup_PreservesAnonymousContractAndOtherEndpoints()
    {
        // Arrange
        using EndpointTestPipeline pipeline = new(map: endpoints =>
        {
            RouteGroupBuilder group = endpoints.MapGroup("/api").RequireAuthorization();
            IEndpointConventionBuilder health = group.MapUptimeHarborHealth();
            health.Add(builder => builder.DisplayName = "health-convention");
            group.MapGet("/private", () => "private");
        });

        // Act
        DefaultHttpContext healthContext = await pipeline.GetAsync("/api/health");
        DefaultHttpContext privateContext = await pipeline.GetAsync("/api/private");

        // Assert
        Assert.Equal(200, healthContext.Response.StatusCode);
        Assert.Equal("health-convention", healthContext.GetEndpoint()!.DisplayName);
        Assert.Equal(401, privateContext.Response.StatusCode);
    }
}
