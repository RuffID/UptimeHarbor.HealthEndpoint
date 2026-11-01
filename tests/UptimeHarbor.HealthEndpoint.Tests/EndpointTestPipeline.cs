using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace UptimeHarbor.HealthEndpoint.Tests;

internal class EndpointTestPipeline : IDisposable
{
    private readonly RequestDelegate pipeline;
    private readonly ConcurrentDictionary<string, int> executions = new();

    internal EndpointTestPipeline(
        Action<UptimeHarborHealthOptions>? configureHealth = null,
        Action<IServiceCollection>? configureServices = null,
        Action<IEndpointRouteBuilder>? map = null,
        bool responseCaching = false,
        bool outputCaching = false,
        bool registerHealth = true,
        string applicationName = "orders-api",
        string? pathBase = null)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddRouting();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment { ApplicationName = applicationName });
        services.AddSingleton<DiagnosticListener>(new DiagnosticListener("HealthEndpointTests"));
        services.AddSingleton<DiagnosticSource>(provider => provider.GetRequiredService<DiagnosticListener>());
        services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("test", _ => { });
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });
        services.AddResponseCaching();
        services.AddOutputCache(options => options.AddBasePolicy(policy => policy.Cache()));
        if (registerHealth)
        {
            if (configureHealth is null)
            {
                services.AddUptimeHarborHealth();
            }
            else
            {
                services.AddUptimeHarborHealth(configureHealth);
            }
        }

        configureServices?.Invoke(services);
        Services = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        ApplicationBuilder app = new(Services);
        if (pathBase is not null)
        {
            app.UsePathBase(pathBase);
        }

        app.UseRouting();
        app.UseAuthorization();
        if (responseCaching)
        {
            app.UseResponseCaching();
        }

        if (outputCaching)
        {
            app.UseOutputCache();
        }

        app.Use(next => async context =>
        {
            executions.AddOrUpdate(context.Request.Path.Value!, 1, (_, count) => count + 1);
            await next(context);
        });
        app.UseEndpoints(endpoints =>
        {
            if (map is null)
            {
                endpoints.MapUptimeHarborHealth();
            }
            else
            {
                map(endpoints);
            }
        });
        pipeline = app.Build();
    }

    internal ServiceProvider Services { get; }

    internal int Executions(string path)
    {
        return executions.GetValueOrDefault(path);
    }

    internal async Task<DefaultHttpContext> GetAsync(string path = "/health", string method = "GET")
    {
        using IServiceScope scope = Services.CreateScope();

        DefaultHttpContext context = new();
        context.RequestServices = scope.ServiceProvider;
        context.Request.Method = method;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("isolated.invalid");
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        await pipeline(context);

        return context;
    }

    internal static string Body(HttpContext context)
    {
        return Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
    }

    public void Dispose()
    {
        Services.Dispose();
    }
}
