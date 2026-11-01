using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace UptimeHarbor.HealthEndpoint.Tests;

internal static class HealthTestServices
{
    internal static ServiceProvider CreateProvider(
        Action<UptimeHarborHealthOptions>? configureOptions = null,
        string applicationName = "orders-api")
    {
        ServiceCollection services = new();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment
        {
            ApplicationName = applicationName
        });
        if (configureOptions is null)
        {
            services.AddUptimeHarborHealth();
        }
        else
        {
            services.AddUptimeHarborHealth(configureOptions);
        }

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    internal static async Task<UptimeHarborHealthState> InitializeAsync(ServiceProvider services)
    {
        services.GetRequiredService<IStartupValidator>().Validate();
        foreach (IHostedLifecycleService service in services
            .GetServices<IHostedService>()
            .OfType<IHostedLifecycleService>())
        {
            await service.StartingAsync(CancellationToken.None);
        }

        return services.GetRequiredService<UptimeHarborHealthState>();
    }
}
