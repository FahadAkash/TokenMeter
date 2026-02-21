using System.Globalization;

namespace TokenMeter.Core.Parsing;

/// <summary>
/// Parses date strings in a variety of formats — ISO 8601, yyyy-MM-dd, "MMM d, yyyy", etc.
/// Mirrors the Swift <c>CostUsageDateParser</c>.
/// </summary>
public static class DateParser
{
    private static readonly string[] DayFormats =
    [
        "yyyy-MM-dd",
        "MMM d, yyyy",
    ];

    private static readonly string[] MonthFormats =
    [
        "MMM yyyy",
        "MMMM yyyy",
        "yyyy-MM",
    ];

    public static DateTimeOffset? ParseDay(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var trimmed = text.Trim();

        // ISO 8601 (with or without fractional seconds)
        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var iso))
            return iso;

        foreach (var fmt in DayFormats)
        {
            if (DateTimeOffset.TryParseExact(trimmed, fmt, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal, out var d))
                return d;
        }

        return null;
    }

    public static DateTimeOffset? ParseMonth(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var trimmed = text.Trim();

        foreach (var fmt in MonthFormats)
        {
            if (DateTimeOffset.TryParseExact(trimmed, fmt, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal, out var d))
                return d;
        }

        return null;
    }
}
