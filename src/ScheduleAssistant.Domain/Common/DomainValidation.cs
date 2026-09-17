using System.Globalization;
using System.Text;

namespace ScheduleAssistant.Domain;

internal static class DomainValidation
{
    public static Guid RequireNonEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException($"{parameterName} must not be empty.", parameterName);
        }

        return value;
    }

    public static T RequireDefinedEnum<T>(T value, string parameterName)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new DomainValidationException(
                $"{parameterName} has an unsupported value: {Convert.ToInt64(value, CultureInfo.InvariantCulture)}.",
                parameterName);
        }

        return value;
    }

    public static string RequireNonBlank(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException($"{parameterName} must not be blank.", parameterName);
        }

        return value;
    }

    public static string NormalizeTitle(string? value, string parameterName, int maximumLength)
    {
        var title = RequireNonBlank(value, parameterName).Trim();
        EnsureMaximumLength(title, maximumLength, parameterName);
        return title;
    }

    public static string? NormalizeOptionalText(string? value, string parameterName, int maximumLength)
    {
        if (value is null)
        {
            return null;
        }

        EnsureMaximumLength(value, maximumLength, parameterName);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static string NormalizeName(string? value, string parameterName, int maximumLength)
    {
        var name = RequireNonBlank(value, parameterName).Trim();
        EnsureMaximumLength(name, maximumLength, parameterName);
        return name;
    }

    public static string NormalizeTimeZoneId(string? value, string parameterName = "timeZoneId")
    {
        var timeZoneId = RequireNonBlank(value, parameterName).Trim();
        EnsureMaximumLength(timeZoneId, 256, parameterName);

        if (timeZoneId.Any(char.IsControl))
        {
            throw new DomainValidationException($"{parameterName} contains a control character.", parameterName);
        }

        return timeZoneId;
    }

    public static DateTimeOffset NormalizeUtc(DateTimeOffset value, string parameterName)
    {
        return value.ToUniversalTime();
    }

    public static void EnsureMaximumLength(string value, int maximumLength, string parameterName)
    {
        if (CountTextElements(value) > maximumLength)
        {
            throw new DomainValidationException(
                $"{parameterName} must be at most {maximumLength} characters.",
                parameterName);
        }
    }

    public static void EnsureAtOrAfter(DateTimeOffset value, DateTimeOffset minimum, string parameterName)
    {
        if (value < minimum)
        {
            throw new DomainValidationException(
                $"{parameterName} must not be earlier than the creation timestamp.",
                parameterName);
        }
    }

    public static int CountTextElements(string value)
    {
        return value.EnumerateRunes().Count();
    }

    public static string? NormalizeMetadata(string? value, string parameterName, int maximumLength)
    {
        if (value is null || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        EnsureMaximumLength(normalized, maximumLength, parameterName);
        return normalized;
    }

    public static void EnsureNoControlCharacters(string value, string parameterName)
    {
        if (value.Any(char.IsControl))
        {
            throw new DomainValidationException($"{parameterName} contains a control character.", parameterName);
        }
    }

    public static string? NormalizeHash(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var hash = value.Trim();
        if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new DomainValidationException($"{parameterName} must be a SHA-256 hexadecimal value.", parameterName);
        }

        return hash.ToLowerInvariant();
    }

    public static string NormalizeColorHex(string? value, string parameterName)
    {
        var color = RequireNonBlank(value, parameterName).Trim();
        if (color.Length != 7 || color[0] != '#' || color.Skip(1).Any(character => !Uri.IsHexDigit(character)))
        {
            throw new DomainValidationException(
                $"{parameterName} must use the #RRGGBB format.",
                parameterName);
        }

        return color.ToUpperInvariant();
    }

    public static string? NormalizeExtension(string? value, string parameterName)
    {
        var extension = NormalizeMetadata(value, parameterName, 32);
        if (extension is null)
        {
            return null;
        }

        if (extension[0] != '.' || extension.Length == 1 || extension.Any(character => !IsExtensionCharacter(character)))
        {
            throw new DomainValidationException(
                $"{parameterName} must be a file extension beginning with a period.",
                parameterName);
        }

        return extension.ToLowerInvariant();
    }

    private static bool IsExtensionCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value is '.' or '+' or '-' or '_';
    }
}
