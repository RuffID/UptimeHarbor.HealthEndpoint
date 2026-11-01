using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using UptimeHarbor.HealthEndpoint;
using UptimeHarbor.HealthEndpoint.Models;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Подключение endpoint мониторинга к существующему pipeline приложения.
/// </summary>
public static class UptimeHarborHealthEndpointRouteBuilderExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    /// <summary>
    /// Подключает анонимный GET с неизменяемыми сведениями о процессе; требует AddUptimeHarborHealth.
    /// </summary>
    public static IEndpointConventionBuilder MapUptimeHarborHealth(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        UptimeHarborHealthState? state = endpoints.ServiceProvider.GetService<UptimeHarborHealthState>();
        if (state is null)
        {
            throw new InvalidOperationException(
                "Call AddUptimeHarborHealth before MapUptimeHarborHealth.");
        }

        UptimeHarborHealthResponse response = new(state.Name, state.StartedAtUtc);
        // Routing сопоставляет декодированный Request.Path; снимок исходной настройки не меняется.
        string route = PathString.FromUriComponent(state.Path).Value!;
        IEndpointConventionBuilder builder = endpoints.MapGet(route, (HttpContext context) =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.Headers.CacheControl = "no-store";

            return context.Response.WriteAsJsonAsync(response, JsonOptions, context.RequestAborted);
        });

        // Завершающая convention сохраняет запрет кеша и anonymous после обычных conventions приложения.
        builder.Finally(endpoint =>
        {
            endpoint.Metadata.Add(new AllowAnonymousAttribute());
            endpoint.Metadata.Add(UptimeHarborHealthNoCachePolicy.Instance);
        });

        return builder;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.General);
        options.MakeReadOnly(populateMissingResolver: true);

        return options;
    }
}
