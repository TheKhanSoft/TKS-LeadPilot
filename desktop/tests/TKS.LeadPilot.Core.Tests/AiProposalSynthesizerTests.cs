using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.Core.Services;
using Xunit;

namespace TKS.LeadPilot.Core.Tests;

public class AiProposalSynthesizerTests
{
    private class FakeSecretVault : ISecretVault
    {
        private readonly Dictionary<string, string> _secrets = new();

        public void SaveSecret(string key, string secret) => _secrets[key] = secret;
        public string? GetSecret(string key) => _secrets.TryGetValue(key, out var val) ? val : null;
        public void DeleteSecret(string key) => _secrets.Remove(key);
        public bool HasSecret(string key) => _secrets.ContainsKey(key);
    }

    private class MockHttpMessageHandler(string responseContent, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int CallCount { get; private set; } = 0;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private static WebsiteAudit CreateSampleAudit()
    {
        var audit = new WebsiteAudit
        {
            Id = Guid.NewGuid().ToString(),
            TargetUrl = "https://apexclinics.pk",
            OverallScore = 48,
            PerformanceScore = 40,
            SecurityScore = 55,
            SeoScore = 50,
            TtfbMs = 1450,
            SslValid = true,
            SslDaysUntilExpiry = 18,
            HasRobotsTxt = false,
            HasSitemapXml = false,
            DetectedTechStack = "WordPress, WooCommerce"
        };

        audit.Findings.Add(new WebsiteAuditFinding
        {
            Category = AuditCategory.Performance,
            Severity = AuditSeverity.Critical,
            RuleCode = "PERF_HIGH_TTFB",
            Title = "Server response time exceeds 1,400ms",
            Description = "Initial server response latency is 1,450ms, causing high mobile bounce rates.",
            RecommendedFix = "Migrate to high-performance managed cloud host with edge caching.",
            BusinessImpact = "Estimated 30-40% visitor drop-off before first paint."
        });

        audit.Findings.Add(new WebsiteAuditFinding
        {
            Category = AuditCategory.Security,
            Severity = AuditSeverity.Medium,
            RuleCode = "SEC_SSL_EXPIRY_SOON",
            Title = "SSL Certificate expires in less than 30 days",
            Description = "TLS Certificate expires in 18 days without automated renewal detected.",
            RecommendedFix = "Configure automated ACME / Let's Encrypt renewal script."
        });

        return audit;
    }

    [Fact]
    public void RedactPii_ScrubsPhoneNumbersAndEmailsFromText()
    {
        var text = "Contact Dr. Tariq at +92 300 1234567 or tariq.apex@gmail.com for inquiries at Apex Dental.";
        var scrubbed = AiProposalSynthesizer.RedactPii(text);

        scrubbed.Should().NotContain("+92 300 1234567");
        scrubbed.Should().NotContain("tariq.apex@gmail.com");
        scrubbed.Should().Contain("[PHONE REDACTED]");
        scrubbed.Should().Contain("[EMAIL REDACTED]");
        scrubbed.Should().Contain("Apex Dental");
    }

    [Fact]
    public async Task SynthesizeProposalAsync_WhenAiDisabled_ReturnsDeterministicFallback()
    {
        var mockHttp = new MockHttpMessageHandler("{}");
        var httpClient = new HttpClient(mockHttp);
        var vault = new FakeSecretVault();
        vault.SaveSecret("GEMINI_API_KEY", "AIzaSyFakeKey");

        var synthesizer = new AiProposalSynthesizer(httpClient, vault);
        await synthesizer.SaveConfigAsync(new GeminiConfig { IsEnabled = false });

        var request = new AuditProposalSynthesisRequest
        {
            Business = new Business { Name = "Apex Dental", City = "Karachi" },
            Audit = CreateSampleAudit(),
            Tone = AiTone.Consultative
        };

        var result = await synthesizer.SynthesizeProposalAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.IsLocalFallback.Should().BeTrue();
        result.FullProposalMarkdown.Should().Contain("Apex Dental");
        result.ErrorMessage.Should().Contain("disabled");
        mockHttp.CallCount.Should().Be(0); // Zero network calls
    }

    [Fact]
    public async Task SynthesizeProposalAsync_WhenApiKeyMissing_ReturnsDeterministicFallback()
    {
        var mockHttp = new MockHttpMessageHandler("{}");
        var httpClient = new HttpClient(mockHttp);
        var vault = new FakeSecretVault(); // No key

        var synthesizer = new AiProposalSynthesizer(httpClient, vault);
        await synthesizer.SaveConfigAsync(new GeminiConfig { IsEnabled = true });

        var request = new AuditProposalSynthesisRequest
        {
            Business = new Business { Name = "Apex Dental", City = "Karachi" },
            Audit = CreateSampleAudit(),
            Tone = AiTone.RoiFocused
        };

        var result = await synthesizer.SynthesizeProposalAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.IsLocalFallback.Should().BeTrue();
        result.ErrorMessage.Should().Contain("API key is not configured");
        mockHttp.CallCount.Should().Be(0);
    }

    [Fact]
    public void SynthesizeDeterministicProposal_GeneratesMultiToneProposalsOffline()
    {
        var mockHttp = new MockHttpMessageHandler("{}");
        var synthesizer = new AiProposalSynthesizer(new HttpClient(mockHttp), new FakeSecretVault());

        var audit = CreateSampleAudit();
        var biz = new Business { Name = "Apex Dental", City = "Karachi", WebsiteUrl = "https://apexclinics.pk" };

        // Test Consultative Tone
        var req1 = new AuditProposalSynthesisRequest { Business = biz, Audit = audit, Tone = AiTone.Consultative };
        var res1 = synthesizer.SynthesizeDeterministicProposal(req1);
        res1.FullProposalMarkdown.Should().Contain("Website Optimization & Growth Assessment");
        res1.MatchedServices.Should().Contain("TKS High-Speed Web Redesign & Cloud Modernization");

        // Test ROI-Focused Tone
        var req2 = new AuditProposalSynthesisRequest { Business = biz, Audit = audit, Tone = AiTone.RoiFocused };
        var res2 = synthesizer.SynthesizeDeterministicProposal(req2);
        res2.FullProposalMarkdown.Should().Contain("Commercial Performance & Website Health Proposal");
        res2.FullProposalMarkdown.Should().Contain("Revenue Impact");

        // Test Technical Tone
        var req3 = new AuditProposalSynthesisRequest { Business = biz, Audit = audit, Tone = AiTone.Technical };
        var res3 = synthesizer.SynthesizeDeterministicProposal(req3);
        res3.FullProposalMarkdown.Should().Contain("Architecture, Security & Core Web Vitals Audit");
    }

    [Fact]
    public async Task SynthesizeProposalAsync_SuccessfulGeminiResponse_ParsesContentCorrectly()
    {
        var geminiResponseJson = """
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  {
                    "text": "# Executive Modernization Proposal for Apex Dental\n\n## 1. Executive Summary\nYour website latency is currently driving potential patients away.\n\n## 2. Diagnostic Insights\nWe identified 1,450ms TTFB on your WordPress server.\n\n## 3. Remediation Plan\nTKS can deploy a cloud-edge architecture in 72 hours."
                  }
                ]
              },
              "finishReason": "STOP"
            }
          ],
          "usageMetadata": {
            "promptTokenCount": 540,
            "candidatesTokenCount": 210,
            "totalTokenCount": 750
          }
        }
        """;

        var mockHttp = new MockHttpMessageHandler(geminiResponseJson);
        var httpClient = new HttpClient(mockHttp);
        var vault = new FakeSecretVault();
        vault.SaveSecret("GEMINI_API_KEY", "AIzaSyValidGeminiKey");

        var synthesizer = new AiProposalSynthesizer(httpClient, vault);
        await synthesizer.SaveConfigAsync(new GeminiConfig { IsEnabled = true, DailyRequestCeiling = 100 });

        var request = new AuditProposalSynthesisRequest
        {
            Business = new Business { Name = "Apex Dental", City = "Karachi", WebsiteUrl = "https://apexclinics.pk" },
            Audit = CreateSampleAudit(),
            Tone = AiTone.Consultative
        };

        var result = await synthesizer.SynthesizeProposalAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.IsLocalFallback.Should().BeFalse();
        result.TokensUsed.Should().Be(750);
        result.FullProposalMarkdown.Should().Contain("Executive Modernization Proposal for Apex Dental");
        mockHttp.CallCount.Should().Be(1);
    }
}
