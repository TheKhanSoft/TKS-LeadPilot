using System;
using TKS.LeadPilot.Core.Common;
using Xunit;

namespace TKS.LeadPilot.Core.Tests;

public class TimeHelperTests
{
    [Fact]
    public void PktTimeZone_IsUtcPlusFive()
    {
        var tz = TimeHelper.PktTimeZone;
        Assert.NotNull(tz);
        Assert.Equal(TimeSpan.FromHours(5), tz.BaseUtcOffset);
    }

    [Fact]
    public void ToPkt_ConvertsUtcToPktCorrectly()
    {
        var utc = new DateTime(2026, 11, 19, 5, 0, 0, DateTimeKind.Utc);
        var pkt = utc.ToPkt();

        Assert.Equal(2026, pkt.Year);
        Assert.Equal(11, pkt.Month);
        Assert.Equal(19, pkt.Day);
        Assert.Equal(10, pkt.Hour); // 5 + 5 = 10
        Assert.Equal(0, pkt.Minute);
    }

    [Fact]
    public void ToPktString_FormatsCorrectly()
    {
        var utc = new DateTime(2026, 11, 19, 5, 30, 45, DateTimeKind.Utc);
        var formatted = utc.ToPktString("yyyy-MM-dd HH:mm:ss");

        Assert.Equal("2026-11-19 10:30:45", formatted);
    }

    [Fact]
    public void ToPktString_NullableHandling()
    {
        DateTime? nullDate = null;
        DateTime? validDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal("N/A", nullDate.ToPktString(defaultFallback: "N/A"));
        Assert.Equal("2026-01-01 05:00:00", validDate.ToPktString("yyyy-MM-dd HH:mm:ss"));
    }

    [Fact]
    public void NowPkt_MatchesUtcPlusFiveHours()
    {
        var utcNow = DateTime.UtcNow;
        var pktNow = TimeHelper.NowPkt;

        var diff = pktNow - utcNow;
        // Should be approximately 5 hours (allowing small sub-second execution variance)
        Assert.True(Math.Abs((diff - TimeSpan.FromHours(5)).TotalSeconds) < 2.0);
    }
}
