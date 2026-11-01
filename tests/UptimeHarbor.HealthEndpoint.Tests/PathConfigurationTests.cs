using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace UptimeHarbor.HealthEndpoint.Tests;

public class PathConfigurationTests
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/monitoring/health")]
    [InlineData("/")]
    [InlineData("/Health/")]
    [InlineData("/мониторинг/статус")]
    [InlineData("/health%2Dlive")]
    public async Task Initialize_WithConcretePath_PreservesConfiguredPath(string path)
    {
        // Arrange
        using ServiceProvider services = HealthTestServices.CreateProvider(options =>
        {
            options.Path = path;
        });

        // Act
        UptimeHarborHealthState state = await HealthTestServices.InitializeAsync(services);

        // Assert
        Assert.Equal(path, state.Path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("health")]
    [InlineData(" /health")]
    [InlineData("/health ")]
    [InlineData("https://api.example.com/health")]
    [InlineData("//api.example.com/health")]
    [InlineData("/health:43871")]
    [InlineData("/health?full=true")]
    [InlineData("/health#status")]
    [InlineData("/health/{id}")]
    [InlineData("/health/{**rest}")]
    [InlineData("/health\\status")]
    [InlineData("/health\n")]
    [InlineData("/%7Bid%7D")]
    [InlineData("/%2Fapi.example.com")]
    [InlineData("/health%3Ffull=true")]
    [InlineData("/health%23status")]
    [InlineData("/health%20status")]
    [InlineData("/health%")]
    [InlineData("/health%2")]
    [InlineData("/health%GG")]
    public void StartupValidation_WithInvalidPath_FailsBeforeAnyRequest(string? path)
    {
        // Arrange
        using ServiceProvider services = HealthTestServices.CreateProvider(options =>
        {
            // null намеренно моделирует некорректную внешнюю конфигурацию.
            options.Path = path!;
        });

        // Act
        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
        {
            services.GetRequiredService<IStartupValidator>().Validate();
        });

        // Assert
        Assert.Contains("Path must be a concrete path", exception.Message);
    }

    [Fact]
    public void StartupValidation_WithMultipleErrors_ReportsRulesWithoutConfiguredValues()
    {
        // Arrange
        string secret = "never-log-this-value";
        using ServiceProvider services = HealthTestServices.CreateProvider(options =>
        {
            options.Name = secret + new string('x', 256);
            options.Path = "/health?token=" + secret;
        });

        // Act
        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
        {
            services.GetRequiredService<IStartupValidator>().Validate();
        });

        // Assert
        Assert.Equal(2, exception.Failures.Count());
        Assert.DoesNotContain(secret, exception.Message);
    }
}
