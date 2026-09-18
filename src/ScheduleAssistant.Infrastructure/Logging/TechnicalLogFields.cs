using System.Collections.ObjectModel;
using System.Globalization;

namespace ScheduleAssistant.Infrastructure.Logging;

internal static class TechnicalLogFields
{
    private static readonly HashSet<string> AllowedFieldNames = new(StringComparer.Ordinal)
    {
        "Operation",
        "Component",
        "ErrorCode",
        "Status",
        "PathKind",
        "TaskId",
        "CategoryId",
        "SeriesId",
        "ReminderId",
        "AttachmentId",
        "CorrelationId",
        "Version",
        "ExpectedVersion",
        "ActualVersion",
        "SchemaVersion",
        "MigrationVersion",
        "DurationMs",
        "Count",
        "RetryCount",
        "QueueDepth",
        "FileNumber",
        "BytesWritten",
        "ExceptionHResult"
    };

    private static readonly HashSet<string> TokenFieldNames = new(StringComparer.Ordinal)
    {
        "Operation",
        "Component",
        "ErrorCode",
        "Status",
        "PathKind"
    };

    public static IReadOnlyDictionary<string, string> Extract<TState>(TState state, Exception? exception)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        if (state is IEnumerable<KeyValuePair<string, object?>> values)
        {
            foreach (var pair in values)
            {
                if (string.Equals(pair.Key, "{OriginalFormat}", StringComparison.Ordinal)
                    || !AllowedFieldNames.Contains(pair.Key)
                    || !TryFormatValue(pair.Key, pair.Value, out var formattedValue))
                {
                    continue;
                }

                fields[pair.Key] = formattedValue;
            }
        }

        if (exception is not null)
        {
            fields["ExceptionType"] = exception.GetType().FullName ?? exception.GetType().Name;
            fields["ExceptionHResult"] = exception.HResult.ToString(CultureInfo.InvariantCulture);
        }

        return new ReadOnlyDictionary<string, string>(fields);
    }

    public static string SanitizeCategory(string categoryName)
    {
        return TryFormatToken(categoryName, out var category) ? category : "unknown";
    }

    private static bool TryFormatValue(string fieldName, object? value, out string formattedValue)
    {
        formattedValue = string.Empty;
        if (value is null)
        {
            return false;
        }

        if (value is string stringValue)
        {
            return TokenFieldNames.Contains(fieldName) && TryFormatToken(stringValue, out formattedValue);
        }

        if (value is Guid guid)
        {
            formattedValue = guid.ToString("D", CultureInfo.InvariantCulture);
            return true;
        }

        if (value is DateTimeOffset dateTimeOffset)
        {
            formattedValue = dateTimeOffset.ToUniversalTime().UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
            return true;
        }

        if (value is DateTime dateTime)
        {
            formattedValue = dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            return true;
        }

        if (value is bool boolean)
        {
            formattedValue = boolean ? "true" : "false";
            return true;
        }

        if (value is byte or sbyte or short or ushort or int or uint or long or ulong
            or float or double or decimal)
        {
            formattedValue = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return formattedValue.Length > 0
                && !string.Equals(formattedValue, "NaN", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(formattedValue, "Infinity", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(formattedValue, "-Infinity", StringComparison.OrdinalIgnoreCase);
        }

        if (value.GetType().IsEnum)
        {
            return TryFormatToken(value.ToString() ?? string.Empty, out formattedValue);
        }

        return false;
    }

    private static bool TryFormatToken(string value, out string formattedValue)
    {
        formattedValue = string.Empty;
        if (value.Length is 0 or > 128)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsLetterOrDigit(character)
                && character is not ('.' or '-' or '_' or ':' or '+' or '`'))
            {
                return false;
            }
        }

        formattedValue = value;
        return true;
    }
}
