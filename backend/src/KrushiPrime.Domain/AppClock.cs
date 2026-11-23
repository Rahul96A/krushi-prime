namespace KrushiPrime.Domain;

/// <summary>Krushi Prime is an India-first product: business dates are kept in IST regardless of server timezone.</summary>
public static class AppClock
{
    private static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);
    public static Func<DateTime> UtcNowProvider { get; set; } = () => DateTime.UtcNow;
    public static DateTime Now => DateTime.SpecifyKind(UtcNowProvider().Add(IstOffset), DateTimeKind.Unspecified);
    public static DateTime Today => Now.Date;

    /// <summary>Indian financial year label, e.g. "2026-27".</summary>
    public static string FinancialYear(DateTime d)
    {
        var start = d.Month >= 4 ? d.Year : d.Year - 1;
        return $"{start}-{(start + 1) % 100:D2}";
    }
}

public class BusinessException : Exception
{
    public int StatusCode { get; }
    public BusinessException(string message, int statusCode = 400) : base(message) => StatusCode = statusCode;
}
