using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace UptimeHarbor.HealthEndpoint.Tests;

public class EndpointCachingTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Get_WithStandardCaches_AlwaysExecutesWhileOrdinaryEndpointIsCached(
        bool responseCaching,
        bool outputCaching)
    {
        // Arrange
        using EndpointTestPipeline pipeline = new(responseCaching: responseCaching, outputCaching: outputCaching,
            map: endpoints =>
            {
                IEndpointConventionBuilder health = endpoints.MapUptimeHarborHealth();
                health.CacheOutput(policy => policy.Cache());
                endpoints.MapGet("/cache-control", async (HttpContext context) =>
                {
                    context.Response.Headers.CacheControl = "public,max-age=60";
                    context.Response.ContentLength = 7;
                    await context.Response.WriteAsync("control");
                }).AllowAnonymous().CacheOutput();
            });

        // Act
        DefaultHttpContext first = await pipeline.GetAsync();
        DefaultHttpContext second = await pipeline.GetAsync();
        DefaultHttpContext controlFirst = await pipeline.GetAsync("/cache-control");
        DefaultHttpContext controlSecond = await pipeline.GetAsync("/cache-control");

        // Assert
        Assert.Equal(200, first.Response.StatusCode);
        Assert.Equal(200, second.Response.StatusCode);
        Assert.Equal("no-store", second.Response.Headers.CacheControl);
        Assert.Equal(2, pipeline.Executions("/health"));
        Assert.Equal(EndpointTestPipeline.Body(first), EndpointTestPipeline.Body(second));
        Assert.Equal("control", EndpointTestPipeline.Body(controlFirst));
        Assert.Equal("control", EndpointTestPipeline.Body(controlSecond));
        Assert.Equal(1, pipeline.Executions("/cache-control"));
    }
}
