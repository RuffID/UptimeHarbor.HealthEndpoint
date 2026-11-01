namespace UptimeHarbor.HealthEndpoint;

/// <summary>
/// Настройки имени приложения и пути endpoint мониторинга.
/// </summary>
public class UptimeHarborHealthOptions
{
    /// <summary>
    /// Явное имя длиной 3–256 после Trim; null означает имя приложения хоста.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Конкретный путь внутри приложения, без полного URL и параметров маршрута.
    /// </summary>
    public string Path { get; set; } = "/health";
}
