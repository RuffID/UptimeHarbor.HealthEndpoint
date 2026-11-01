using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using UptimeHarbor.Application.Monitoring;
using UptimeHarbor.Application.Monitoring.Models;
using UptimeHarbor.Infrastructure.Monitoring;
using Xunit;

namespace UptimeHarbor.HealthEndpoint.Tests;

public class ConsumerCompatibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualEndpointPayload_IsAcceptedByServerAndContainerChecker(bool automaticName)
    {
        // Arrange
        string longName = new string('я', 254) + "😀suffix";
        string explicitName = "  Заказы / восток: \"API\" #1 😀  ";
        string expectedName = explicitName.Trim();
        Action<UptimeHarborHealthOptions>? configure = options => options.Name = explicitName;
        if (automaticName)
        {
            configure = null;
            expectedName = longName[..256];
        }

        using EndpointTestPipeline pipeline = new(configure, applicationName: longName);
        DefaultHttpContext context = await pipeline.GetAsync();
        byte[] payload = Encoding.UTF8.GetBytes(EndpointTestPipeline.Body(context));
        using ConsumerCheckerHarness consumer = new(payload);

        // Act
        ServerEndpointCheckResult server = await consumer.Checker.CheckAsync(
            ConsumerCheckerHarness.ENDPOINT_URL, TestContext.Current.CancellationToken);
        ContainerEndpointCheckResult container = await ((IContainerEndpointChecker)consumer.Checker)
            .CheckAsync(ConsumerCheckerHarness.ENDPOINT_URL, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(server.Success);
        Assert.True(container.Success);
        Assert.Null(server.FailureKind);
        Assert.Null(container.FailureKind);
        Assert.Equal(expectedName, server.Name);
        Assert.Equal(expectedName, container.Name);
        Assert.Equal(pipeline.Services.GetRequiredService<UptimeHarborHealthState>().StartedAtUtc, server.StartedAtUtc);
        Assert.Equal(server.StartedAtUtc, container.StartedAtUtc);
        Assert.True(payload.Length < HttpMonitoringEndpointChecker.MAXIMUM_RESPONSE_BODY_BYTES);
    }

    [Theory]
    [InlineData("Z")]
    [InlineData("+00:00")]
    public async Task Consumer_AcceptsBothExplicitUtcSuffixes(string suffix)
    {
        // Arrange
        string body = "{\"name\":\"orders-api\",\"startedAtUtc\":\"2020-01-01T00:00:00" + suffix + "\"}";
        using ConsumerCheckerHarness consumer = new(Encoding.UTF8.GetBytes(body));

        // Act
        ServerEndpointCheckResult result = await consumer.Checker.CheckAsync(
            ConsumerCheckerHarness.ENDPOINT_URL, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(DateTimeOffset.Parse("2020-01-01T00:00:00Z", CultureInfo.InvariantCulture), result.StartedAtUtc);
    }

    [Theory]
    [InlineData("[]", EndpointCheckFailureKind.InvalidName)]
    [InlineData("{\"Name\":\"orders-api\",\"startedAtUtc\":\"2020-01-01T00:00:00Z\"}", EndpointCheckFailureKind.InvalidName)]
    [InlineData("{\"name\":123,\"startedAtUtc\":\"2020-01-01T00:00:00Z\"}", EndpointCheckFailureKind.InvalidName)]
    [InlineData("{\"name\":\"orders-api\",\"startedAtUtc\":\"2020-01-01T00:00:00\"}", EndpointCheckFailureKind.InvalidStartedAtUtc)]
    [InlineData("{\"name\":\"orders-api\",\"startedAtUtc\":\"2020-01-01T00:00:00+03:00\"}", EndpointCheckFailureKind.InvalidStartedAtUtc)]
    [InlineData("{\"name\":\"orders-api\",\"startedAtUtc\":\"9999-01-01T00:00:00Z\"}", EndpointCheckFailureKind.InvalidStartedAtUtc)]
    [InlineData("{\"name\":\"orders-api\",\"StartedAtUtc\":\"2020-01-01T00:00:00Z\"}", EndpointCheckFailureKind.InvalidStartedAtUtc)]
    [InlineData("{\"name\":\"orders-api\",\"startedAtUtc\":123}", EndpointCheckFailureKind.InvalidStartedAtUtc)]
    public async Task Consumer_RejectsBrokenContract(string body, EndpointCheckFailureKind expectedFailure)
    {
        // Arrange
        using ConsumerCheckerHarness consumer = new(Encoding.UTF8.GetBytes(body));

        // Act
        ServerEndpointCheckResult result = await consumer.Checker.CheckAsync(
            ConsumerCheckerHarness.ENDPOINT_URL, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(expectedFailure, result.FailureKind);
    }

    [Theory]
    [InlineData(true, 65536, true)]
    [InlineData(false, 65536, true)]
    [InlineData(true, 65537, false)]
    [InlineData(false, 65537, false)]
    public async Task Consumer_EnforcesRealBodySizeLimit(bool advertiseLength, int size, bool expectedSuccess)
    {
        // Arrange
        const string BODY = "{\"name\":\"orders-api\",\"startedAtUtc\":\"2020-01-01T00:00:00Z\"}";
        byte[] payload = Encoding.UTF8.GetBytes(BODY.PadRight(size));
        using ConsumerCheckerHarness consumer = new(payload, advertiseLength);

        // Act
        ServerEndpointCheckResult result = await consumer.Checker.CheckAsync(
            ConsumerCheckerHarness.ENDPOINT_URL, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedSuccess, result.Success);
        if (!expectedSuccess)
        {
            Assert.Equal(EndpointCheckFailureKind.ResponseTooLarge, result.FailureKind);
        }
    }

    [Fact]
    public async Task LongEscapedExplicitName_RemainsWithinConsumerBodyLimit()
    {
        // Arrange
        string name = new('\u0001', 256);
        using EndpointTestPipeline pipeline = new(options => options.Name = name);

        // Act
        DefaultHttpContext context = await pipeline.GetAsync();
        byte[] payload = Encoding.UTF8.GetBytes(EndpointTestPipeline.Body(context));
        using ConsumerCheckerHarness consumer = new(payload, advertiseLength: false);
        ServerEndpointCheckResult result = await consumer.Checker.CheckAsync(
            ConsumerCheckerHarness.ENDPOINT_URL, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(256, result.Name!.Length);
        Assert.Equal(name, result.Name);
        Assert.True(result.Success);
        Assert.InRange(payload.Length, 1, 2048);
        Assert.True(payload.Length < HttpMonitoringEndpointChecker.MAXIMUM_RESPONSE_BODY_BYTES);
    }

    [Fact]
    public async Task LateMappingAndRepeatedRequests_ReportActualProcessStart()
    {
        // Arrange
        using Process process = Process.GetCurrentProcess();
        DateTimeOffset expectedStart = new(process.StartTime.ToUniversalTime());
        DateTimeOffset beforeMapping = DateTimeOffset.UtcNow;
        using EndpointTestPipeline pipeline = new();

        // Act
        DefaultHttpContext first = await pipeline.GetAsync();
        DefaultHttpContext second = await pipeline.GetAsync();
        using JsonDocument json = JsonDocument.Parse(EndpointTestPipeline.Body(second));
        DateTimeOffset actual = DateTimeOffset.Parse(json.RootElement.GetProperty("startedAtUtc").GetString()!,
            CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(expectedStart, actual);
        Assert.Equal(TimeSpan.Zero, actual.Offset);
        Assert.True(actual <= beforeMapping);
        Assert.Equal(EndpointTestPipeline.Body(first), EndpointTestPipeline.Body(second));
    }
}
