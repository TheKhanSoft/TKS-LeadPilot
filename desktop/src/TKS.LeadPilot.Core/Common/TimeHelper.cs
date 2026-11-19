using System;

namespace TKS.LeadPilot.Core.Common;

/// <summary>
/// Centralized time helper for consistent Pakistan Standard Time (PKT, UTC+05:00) formatting and conversion.
/// </summary>
public static class TimeHelper
{
    private static readonly Lazy<TimeZoneInfo> PktZoneLazy = new(() =>
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Karachi");
            }
            catch
            {
                return TimeZoneInfo.CreateCustomTimeZone("PKT", TimeSpan.FromHours(5), "(UTC+05:00) Pakistan Standard Time", "PKT");
            }
        }
    });

    public static TimeZoneInfo PktTimeZone => PktZoneLazy.Value;

    /// <summary>
    /// Current wall-clock time in Pakistan Standard Time (PKT).
    /// </summary>
    public static DateTime NowPkt => ToPkt(DateTime.UtcNow);

    /// <summary>
    /// Converts a DateTime (assumed UTC if Unspecified) to Pakistan Standard Time.
    /// </summary>
    public static DateTime ToPkt(this DateTime dt)
    {
        var utc = dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utc, PktTimeZone);
    }

    /// <summary>
    /// Converts a nullable DateTime to Pakistan Standard Time.
    /// </summary>
    public static DateTime? ToPkt(this DateTime? dt)
    {
        if (!dt.HasValue) return null;
        return ToPkt(dt.Value);
    }

    /// <summary>
    /// Formats DateTime as a PKT string with the specified format.
    /// </summary>
    public static string ToPktString(this DateTime dt, string format = "yyyy-MM-dd HH:mm:ss")
    {
        return ToPkt(dt).ToString(format);
    }

    /// <summary>
    /// Formats nullable DateTime as a PKT string with the specified format, or returns defaultFallback.
    /// </summary>
    public static string ToPktString(this DateTime? dt, string format = "yyyy-MM-dd HH:mm:ss", string defaultFallback = "N/A")
    {
        if (!dt.HasValue) return defaultFallback;
        return ToPkt(dt.Value).ToString(format);
    }
}
