using Serilog.Core;
using Serilog.Events;
using System.Text.RegularExpressions;

namespace HealthPlatform.Api.Logging;

/// <summary>
/// Serilog enricher that sanitises string property values for PHI before the
/// event reaches any sink. Operates on all ScalarValue string properties.
/// </summary>
public sealed partial class PhiSanitizingEnricher : ILogEventEnricher
{
    [GeneratedRegex(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(\+?1?\s?)?(\(?\d{3}\)?[\s.\-]?)(\d{3}[\s.\-]?\d{4})", RegexOptions.Compiled)]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"\b(19|20)\d{2}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])\b", RegexOptions.Compiled)]
    private static partial Regex DobRegex();

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var property in logEvent.Properties.ToList())
        {
            if (property.Value is ScalarValue { Value: string str })
            {
                var sanitised = Sanitise(str);
                if (sanitised != str)
                {
                    logEvent.AddOrUpdateProperty(
                        propertyFactory.CreateProperty(property.Key, sanitised));
                }
            }
        }
    }

    private static string Sanitise(string value) =>
        DobRegex().Replace(
            PhoneRegex().Replace(
                EmailRegex().Replace(value, "[email-redacted]"),
                "[phone-redacted]"),
            "[dob-redacted]");
}
