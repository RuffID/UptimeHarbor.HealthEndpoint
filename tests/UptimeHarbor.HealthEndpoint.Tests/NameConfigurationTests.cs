using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace UptimeHarbor.HealthEndpoint.Tests;

public class NameConfigurationTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(200)]
    [InlineData(255)]
    [InlineData(256)]
    public async Task Initialize_WithValidExplicitName_TrimsAndPreservesName(int length)
    {
        // Arrange
        string expectedName = new('я', length);
        using ServiceProvider services = HealthTestServices.CreateProvider(options =>
        {
            options.Name = "  " + expectedName + "  ";
        });

        // Act
        UptimeHarborHealthState state = await HealthTestServices.InitializeAsync(services);

        // Assert
        Assert.Equal(expectedName, state.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(257)]
    public void StartupValidation_WithInvalidExplicitLength_RejectsName(int length)
    {
        // Arrange
        using ServiceProvider services = HealthTestServices.CreateProvider(options =>
        {
            options.Name = new string('x', length);
        });

        // Act
        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
        {
            services.GetRequiredService<IStartupValidator>().Validate();
        });

        // Assert
        Assert.Contains("Name must contain between 3 and 256", exception.Message);
    }

    [Fact]
    public void StartupValidation_WithExplicitWhitespace_DoesNotUseAutomaticName()
    {
        // Arrange
        using ServiceProvider services = HealthTestServices.CreateProvider(options =>
        {
            options.Name = " \t ";
        });

        // Act
        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
        {
            services.GetRequiredService<IStartupValidator>().Validate();
        });

        // Assert
        Assert.Contains("Name must contain", exception.Message);
    }

    [Theory]
    [InlineData("Тестовое приложение")]
    [InlineData("API / заказы: восток #1")]
    [InlineData("App 😀")]
    public async Task Initialize_WithExplicitSymbols_DoesNotImposeCharacterRestrictions(string name)
    {
        // Arrange
        using ServiceProvider services = HealthTestServices.CreateProvider(options =>
        {
            options.Name = name;
        });

        // Act
        UptimeHarborHealthState state = await HealthTestServices.InitializeAsync(services);

        // Assert
        Assert.Equal(name, state.Name);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(256)]
    [InlineData(257)]
    public async Task Initialize_WithoutExplicitName_UsesAndBoundsApplicationName(int length)
    {
        // Arrange
        string applicationName = new('x', length);
        string expectedName = new('x', Math.Min(length, 256));
        using ServiceProvider services = HealthTestServices.CreateProvider(
            applicationName: " " + applicationName + " ");

        // Act
        UptimeHarborHealthState state = await HealthTestServices.InitializeAsync(services);

        // Assert
        Assert.Equal(expectedName, state.Name);
    }

    [Theory]
    [InlineData(254, 256)]
    [InlineData(255, 255)]
    public async Task Initialize_WithLongAutomaticUnicodeName_DoesNotSplitSurrogatePair(
        int prefixLength,
        int expectedLength)
    {
        // Arrange
        string applicationName = new string('x', prefixLength) + "😀z";
        using ServiceProvider services = HealthTestServices.CreateProvider(applicationName: applicationName);

        // Act
        UptimeHarborHealthState state = await HealthTestServices.InitializeAsync(services);

        // Assert
        Assert.Equal(expectedLength, state.Name.Length);
        Assert.Equal(applicationName[..expectedLength], state.Name);
        Assert.False(char.IsHighSurrogate(state.Name[^1]));
    }

    [Fact]
    public async Task Initialize_WhenTruncationExposesTrailingWhitespace_TrimsResult()
    {
        // Arrange
        string applicationName = "x" + new string(' ', 255) + "z";
        using ServiceProvider services = HealthTestServices.CreateProvider(applicationName: applicationName);

        // Act
        UptimeHarborHealthState state = await HealthTestServices.InitializeAsync(services);

        // Assert
        Assert.Equal("x", state.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t ")]
    public void StartupValidation_WithEmptyApplicationName_RejectsInsteadOfGeneratingName(string name)
    {
        // Arrange
        using ServiceProvider services = HealthTestServices.CreateProvider(applicationName: name);

        // Act
        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
        {
            services.GetRequiredService<IStartupValidator>().Validate();
        });

        // Assert
        Assert.Contains("IHostEnvironment.ApplicationName must not be empty", exception.Message);
    }
}
