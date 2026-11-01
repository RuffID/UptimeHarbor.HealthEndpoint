using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace UptimeHarbor.HealthEndpoint;

internal class UptimeHarborHealthOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<UptimeHarborHealthOptions>
{
    public ValidateOptionsResult Validate(string? name, UptimeHarborHealthOptions options)
    {
        List<string> failures = [];
        string resolvedName = UptimeHarborHealthConfiguration.ResolveName(
            options.Name,
            environment.ApplicationName);
        if (options.Name is not null)
        {
            if (resolvedName.Length < UptimeHarborHealthConfiguration.MINIMUM_EXPLICIT_NAME_LENGTH
                || resolvedName.Length > UptimeHarborHealthConfiguration.MAXIMUM_NAME_LENGTH)
            {
                failures.Add("Name must contain between 3 and 256 UTF-16 code units after trimming.");
            }
        }
        else if (resolvedName.Length == 0)
        {
            failures.Add("IHostEnvironment.ApplicationName must not be empty when Name is not configured.");
        }

        if (!UptimeHarborHealthConfiguration.IsValidPath(options.Path))
        {
            failures.Add("Path must be a concrete path starting with a single '/', without a host, port, query, fragment, route parameters, whitespace or invalid escapes.");
        }

        if (failures.Count > 0)
        {
            return ValidateOptionsResult.Fail(failures);
        }

        return ValidateOptionsResult.Success;
    }
}
