using Microsoft.Extensions.Options;
using UptimeHarbor.HealthEndpoint;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Регистрация настроек и сведений о процессе для мониторинга UptimeHarbor.
/// </summary>
public static class UptimeHarborHealthServiceCollectionExtensions
{
    /// <summary>
    /// Подключает библиотеку с именем приложения хоста и путём /health.
    /// </summary>
    public static IServiceCollection AddUptimeHarborHealth(this IServiceCollection services)
    {
        return AddCore(services, null);
    }

    /// <summary>
    /// Подключает библиотеку с явной настройкой имени и пути.
    /// </summary>
    public static IServiceCollection AddUptimeHarborHealth(
        this IServiceCollection services,
        Action<UptimeHarborHealthOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(configureOptions);

        return AddCore(services, configureOptions);
    }

    private static IServiceCollection AddCore(
        IServiceCollection services,
        Action<UptimeHarborHealthOptions>? configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);

        OptionsBuilder<UptimeHarborHealthOptions> options = services
            .AddOptions<UptimeHarborHealthOptions>();
        if (configureOptions is not null)
        {
            options.Configure(configureOptions);
        }

        options.ValidateOnStart();
        services.AddSingleton<IValidateOptions<UptimeHarborHealthOptions>, UptimeHarborHealthOptionsValidator>();
        services.AddSingleton<UptimeHarborHealthState>();
        services.AddHostedService<UptimeHarborHealthStartupService>();

        return services;
    }
}
