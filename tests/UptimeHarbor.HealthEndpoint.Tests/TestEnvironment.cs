using System.Runtime.CompilerServices;

namespace UptimeHarbor.HealthEndpoint.Tests;

internal static class TestEnvironment
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Выключить телеметрию тестового runner до входа в сгенерированный Main.
        Environment.SetEnvironmentVariable("TESTINGPLATFORM_TELEMETRY_OPTOUT", "1");
    }
}
