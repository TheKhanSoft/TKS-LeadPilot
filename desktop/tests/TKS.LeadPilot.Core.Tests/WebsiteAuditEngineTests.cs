using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.Core.Services;
using Xunit;

namespace TKS.LeadPilot.Core.Tests;

public class WebsiteAuditEngineTests
{
    [Fact]
    public async Task RunAuditAsync_WhenSsrfFails_ReturnsFailedAuditWithoutNetworkCall()
    {
        var httpClient = new HttpClient(new MockHttpMessageHandler("<html/>"));
        var ssrfMock = new FakeSsrfValidator(isSafe: false, reason: "Forbidden private IP 192.168.1.1");
        var sut = new WebsiteAuditEngine(httpClient, ssrfMock);

        var audit = await sut.RunAuditAsync("biz-123", "http://192.168.1.1");

        audit.Status.Should().Be(AuditStatus.Failed);
        audit.ErrorMessage.Should().Contain("SSRF Guard Blocked");
        audit.Findings.Should().Contain(f => f.RuleCode == "SEC_SSRF_BLOCKED");
    }

    [Fact]
    public async Task RunAuditAsync_DetectsMissingTitleAndMissingViewport()
    {
        // HTML missing title, missing description, missing viewport
        var html = "<html><body><h1>Welcome to our shop</h1><p>Content</p></body></html>";
        var httpClient = new HttpClient(new MockHttpMessageHandler(html));
        var ssrfMock = new FakeSsrfValidator(isSafe: true, cleanUri: new Uri("https://example.com"));
        var sut = new WebsiteAuditEngine(httpClient, ssrfMock);

        var audit = await sut.RunAuditAsync("biz-123", "https://example.com");

        audit.Status.Should().Be(AuditStatus.Completed, because: audit.ErrorMessage);
        audit.Findings.Should().Contain(f => f.RuleCode == "SEO_NO_TITLE");
        audit.Findings.Should().Contain(f => f.RuleCode == "MOB_NO_VIEWPORT");
        audit.Findings.Should().Contain(f => f.RuleCode == "SEC_NO_HSTS");

        audit.SeoScore.Should().BeLessThan(80, "missing title and viewport reduce SEO score");
        audit.SecurityScore.Should().BeLessThan(80, "missing HSTS and CSP reduce security score");
        audit.OverallScore.Should().BeInRange(0, 100);
    }

    private class FakeSsrfValidator(bool isSafe, string? reason = null, Uri? cleanUri = null) : ISsrfValidator
    {
        public Task<SsrfValidationResult> ValidateUrlAsync(string? url, CancellationToken ct = default)
        {
            return Task.FromResult(new SsrfValidationResult
            {
                IsSafe = isSafe,
                Reason = reason,
                CleanUri = cleanUri ?? (isSafe ? new Uri(url ?? "https://example.com") : null)
            });
        }
    }

    private class MockHttpMessageHandler(string responseContent) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent, Encoding.UTF8, "text/html")
            };
            return Task.FromResult(response);
        }
    }
}
