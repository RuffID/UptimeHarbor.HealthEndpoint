namespace UptimeHarbor.HealthEndpoint;

internal static class UptimeHarborHealthConfiguration
{
    internal const int MINIMUM_EXPLICIT_NAME_LENGTH = 3;
    internal const int MAXIMUM_NAME_LENGTH = 256;

    internal static string ResolveName(string? configuredName, string applicationName)
    {
        if (configuredName is not null)
        {
            return configuredName.Trim();
        }

        string name = applicationName?.Trim() ?? string.Empty;
        if (name.Length <= MAXIMUM_NAME_LENGTH)
        {
            return name;
        }

        int length = MAXIMUM_NAME_LENGTH;
        if (char.IsHighSurrogate(name[length - 1]) && char.IsLowSurrogate(name[length]))
        {
            length--;
        }

        return name[..length].TrimEnd();
    }

    internal static bool IsValidPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        if (!IsConcretePath(path))
        {
            return false;
        }

        // Неверные escape-последовательности не должны превращаться в другой путь.
        int position = 0;
        while (position < path.Length)
        {
            if (path[position] == '%')
            {
                if (position + 2 >= path.Length)
                {
                    return false;
                }

                if (!Uri.IsHexDigit(path[position + 1]) || !Uri.IsHexDigit(path[position + 2]))
                {
                    return false;
                }

                position += 3;
            }
            else
            {
                position++;
            }
        }

        return IsConcretePath(Uri.UnescapeDataString(path));
    }

    private static bool IsConcretePath(string path)
    {
        if (!path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (char character in path)
        {
            bool isReservedSyntax = character is '?' or '#' or '{' or '}' or '\\' or ':';
            if (isReservedSyntax || char.IsWhiteSpace(character) || char.IsControl(character))
            {
                return false;
            }
        }

        return true;
    }
}
