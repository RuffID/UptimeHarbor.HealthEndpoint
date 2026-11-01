using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace UptimeHarbor.HealthEndpoint.Tests;

public class StartupStateTests
{
    [Fact]
    public async Task Initialize_WithoutOverrides_UsesDefaultsAndActualProcessStart()
    {
        // Arrange
        using Process process = Process.GetCurrentProcess();
        DateTimeOffset expectedStart = new(process.StartTime.ToUniversalTime());
        DateTimeOffset beforeInitialization = DateTimeOffset.UtcNow;
        using ServiceProvider services = HealthTestServices.CreateProvider();

        // Act
        UptimeHarborHealthState state = await HealthTestServices.InitializeAsync(services);

        // Assert
        Assert.Equal("orders-api", state.Name);
        Assert.Equal("/health", state.Path);
        Assert.Equal(expectedStart, state.StartedAtUtc);
        Assert.Equal(TimeSpan.Zero, state.StartedAtUtc.Offset);
        Assert.True(state.StartedAtUtc <= beforeInitialization);
    }

    [Fact]
    public async Task StartingPhase_MaterializesSnapshotBeforeHostedServicesStart()
    {
        // Arrange
        int configurationCalls = 0;
        using ServiceProvider services = HealthTestServices.CreateProvider(options =>
        {
            configurationCalls++;
            options.Name = "starting-phase";
        });
        IHostedLifecycleService startup = Assert.Single(services
            .GetServices<IHostedService>()
            .OfType<IHostedLifecycleService>());

        // Act
        await startup.StartingAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, configurationCalls);
        Assert.Equal("starting-phase", services.GetRequiredService<UptimeHarborHealthState>().Name);
    }

    [Fact]
    public async Task Initialize_ThenOptionsChange_KeepsImmutableSingletonSnapshot()
    {
        // Arrange
        using ServiceProvider services = HealthTestServices.CreateProvider();
        UptimeHarborHealthState original = await HealthTestServices.InitializeAsync(services);
        UptimeHarborHealthOptions options = services.GetRequiredService<IOptions<UptimeHarborHealthOptions>>().Value;

        // Act
        options.Name = "changed-name";
        options.Path = "/changed";
        UptimeHarborHealthState repeated = services.GetRequiredService<UptimeHarborHealthState>();

        // Assert
        Assert.Same(original, repeated);
        Assert.Equal("orders-api", repeated.Name);
        Assert.Equal("/health", repeated.Path);
        Assert.Equal(original.StartedAtUtc, repeated.StartedAtUtc);
    }

    [Fact]
    public async Task Initialize_InTwoServiceProviders_ReportsSameProcessStart()
    {
        // Arrange
        using ServiceProvider first = HealthTestServices.CreateProvider();
        using ServiceProvider second = HealthTestServices.CreateProvider();

        // Act
        UptimeHarborHealthState firstState = await HealthTestServices.InitializeAsync(first);
        UptimeHarborHealthState secondState = await HealthTestServices.InitializeAsync(second);

        // Assert
        Assert.NotSame(firstState, secondState);
        Assert.Equal(firstState.StartedAtUtc, secondState.StartedAtUtc);
    }

    [Fact]
    public async Task StartingPhase_WhenCanceled_DoesNotInitializeOptions()
    {
        // Arrange
        int configurationCalls = 0;
        using ServiceProvider services = HealthTestServices.CreateProvider(options =>
        {
            configurationCalls++;
        });
        IHostedLifecycleService startup = Assert.Single(services
            .GetServices<IHostedService>()
            .OfType<IHostedLifecycleService>());

        // Act
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
        {
            return startup.StartingAsync(new CancellationToken(canceled: true));
        });

        // Assert
        Assert.Equal(0, configurationCalls);
    }

    [Fact]
    public void Add_WithoutOverrides_ReturnsOriginalServiceCollection()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        IServiceCollection result = services.AddUptimeHarborHealth();

        // Assert
        Assert.Same(services, result);
    }

    [Fact]
    public void Add_WithNullDelegate_RejectsConfigurationArgument()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
        {
            services.AddUptimeHarborHealth(null!);
        });

        // Assert
        Assert.Equal("configureOptions", exception.ParamName);
    }
}
