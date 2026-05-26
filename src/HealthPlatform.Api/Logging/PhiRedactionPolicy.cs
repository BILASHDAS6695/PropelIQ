using System.Diagnostics.CodeAnalysis;
using Serilog.Core;
using Serilog.Events;
using System.Text.RegularExpressions;

namespace HealthPlatform.Api.Logging;

/// <summary>
/// Serilog destructuring policy that masks PHI fields in log events.
/// Masks: email addresses, US phone numbers, ISO-8601 date-of-birth values.
/// Applied at the pipeline level — call sites need no special treatment.
/// </summary>
public sealed partial class PhiRedactionPolicy : IDestructuringPolicy
{
    [GeneratedRegex(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(\+?1?\s?)?(\(?\d{3}\)?[\s.\-]?)(\d{3}[\s.\-]?\d{4})", RegexOptions.Compiled)]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"\b(19|20)\d{2}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])\b", RegexOptions.Compiled)]
    private static partial Regex DobRegex();

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory,
        [NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        if (value is string str)
        {
            var redacted = DobRegex().Replace(
                PhoneRegex().Replace(
                    EmailRegex().Replace(str, "[email-redacted]"),
                    "[phone-redacted]"),
                "[dob-redacted]");

            if (!ReferenceEquals(redacted, str))
            {
                result = new ScalarValue(redacted);
                return true;
            }
        }

        result = null;
        return false;
    }
}
