using Microsoft.AspNetCore.OutputCaching;

namespace UptimeHarbor.HealthEndpoint;

internal class UptimeHarborHealthNoCachePolicy : IOutputCachePolicy
{
    internal static readonly UptimeHarborHealthNoCachePolicy Instance = new();

    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        // Отключить также lookup: один no-store в ответе не предотвращает выдачу сохранённого тела.
        context.EnableOutputCaching = false;
        context.AllowCacheLookup = false;
        context.AllowCacheStorage = false;
        context.AllowLocking = false;

        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}
