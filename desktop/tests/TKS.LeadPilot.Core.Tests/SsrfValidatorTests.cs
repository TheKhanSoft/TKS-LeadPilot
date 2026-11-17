using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using TKS.LeadPilot.Core.Services;
using Xunit;

namespace TKS.LeadPilot.Core.Tests;

public class SsrfValidatorTests
{
    private readonly SsrfValidator _sut = new();

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("127.0.1.10", true)]
    [InlineData("10.0.0.5", true)]
    [InlineData("172.16.50.1", true)]
    [InlineData("172.31.255.250", true)]
    [InlineData("192.168.1.1", true)]
    [InlineData("169.254.169.254", true)]
    [InlineData("0.0.0.0", true)]
    [InlineData("255.255.255.255", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("1.1.1.1", false)]
    [InlineData("142.250.190.46", false)]
    public void IsPrivateOrRestrictedIp_AccuratelyIdentifiesRestrictedRanges(string ipStr, bool shouldBeRestricted)
    {
        var ip = IPAddress.Parse(ipStr);
        var result = SsrfValidator.IsPrivateOrRestrictedIp(ip);
        result.Should().Be(shouldBeRestricted);
    }

    [Theory]
    [InlineData("http://localhost:8080")]
    [InlineData("https://internal.company.local")]
    [InlineData("ftp://ftp.example.com")]
    [InlineData("file:///C:/Windows/System32")]
    public async Task ValidateUrlAsync_BlocksForbiddenHostnamesAndSchemes(string url)
    {
        var result = await _sut.ValidateUrlAsync(url);
        result.IsSafe.Should().BeFalse();
        result.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ValidateUrlAsync_AllowsPublicResolvableHost()
    {
        var result = await _sut.ValidateUrlAsync("https://www.google.com");
        result.IsSafe.Should().BeTrue();
        result.CleanUri.Should().NotBeNull();
        result.ResolvedIp.Should().NotBeNull();
    }
}
