using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace UptimeHarbor.HealthEndpoint.Tests;

internal class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Tests";

    public string ApplicationName { get; set; } = "orders-api";

    public string ContentRootPath { get; set; } = string.Empty;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
