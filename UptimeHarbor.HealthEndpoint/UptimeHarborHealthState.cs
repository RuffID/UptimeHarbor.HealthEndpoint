using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace UptimeHarbor.HealthEndpoint;

internal class UptimeHarborHealthState
{
    public UptimeHarborHealthState(
        IOptions<UptimeHarborHealthOptions> options,
        IHostEnvironment environment)
    {
        UptimeHarborHealthOptions configuration = options.Value;
        Name = UptimeHarborHealthConfiguration.ResolveName(
            configuration.Name,
            environment.ApplicationName);
        Path = configuration.Path;
        StartedAtUtc = ReadProcessStartTime();
    }

    public string Name { get; }

    public string Path { get; }

    public DateTimeOffset StartedAtUtc { get; }

    private static DateTimeOffset ReadProcessStartTime()
    {
        try
        {
            using Process process = Process.GetCurrentProcess();

            return new DateTimeOffset(process.StartTime.ToUniversalTime());
        }
        catch (Exception exception) when (
            exception is Win32Exception or NotSupportedException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Unable to read the current application process start time.",
                exception);
        }
    }
}
