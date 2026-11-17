using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Core.Services;

public class WebsiteAuditEngine : IWebsiteAuditEngine
{
    private readonly HttpClient _httpClient;
    private readonly ISsrfValidator _ssrfValidator;

    public WebsiteAuditEngine(HttpClient httpClient, ISsrfValidator ssrfValidator)
    {
        _httpClient = httpClient;
        _ssrfValidator = ssrfValidator;
    }

    public async Task<WebsiteAudit> RunAuditAsync(string businessId, string targetUrl, CancellationToken ct = default)
    {
        var audit = new WebsiteAudit
        {
            Id = Guid.NewGuid().ToString(),
            BusinessId = businessId,
            TargetUrl = targetUrl,
            Status = AuditStatus.Running,
            CreatedAtUtc = DateTime.UtcNow
        };

        // 1. SSRF Validation
        var ssrfResult = await _ssrfValidator.ValidateUrlAsync(targetUrl, ct);
        if (!ssrfResult.IsSafe)
        {
            audit.Status = AuditStatus.Failed;
            audit.ErrorMessage = $"SSRF Guard Blocked: {ssrfResult.Reason}";
            audit.Findings.Add(new WebsiteAuditFinding
            {
                AuditId = audit.Id,
                Category = AuditCategory.Security,
                Severity = AuditSeverity.Critical,
                RuleCode = "SEC_SSRF_BLOCKED",
                Title = "Target URL Blocked by Security Policy",
                Description = ssrfResult.Reason ?? "Destination host is restricted.",
                RecommendedFix = "Verify business website address is public and routable."
            });
            audit.CompletedAtUtc = DateTime.UtcNow;
            return audit;
        }

        var uri = ssrfResult.CleanUri!;
        var host = uri.Host;

        try
        {
            // 2. HTTP to HTTPS Redirection Check
            var httpProbeUri = new UriBuilder("http", host).Uri;
            audit.HttpRedirectsToHttps = await CheckHttpToHttpsRedirectAsync(httpProbeUri, ct);

            // 3. SSL Inspection & Security Headers
            var httpsUri = new UriBuilder("https", host).Uri;
            var sw = Stopwatch.StartNew();

            using var request = new HttpRequestMessage(HttpMethod.Get, httpsUri);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36 TKS-LeadPilot/1.0");
            request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            request.Headers.Add("Accept-Encoding", "gzip, deflate, br");

            var sslDetails = await InspectSslCertificateAsync(host);
            audit.SslValid = sslDetails.IsValid;
            audit.SslIssuer = sslDetails.Issuer;
            audit.SslDaysUntilExpiry = sslDetails.DaysUntilExpiry;

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (Exception ex)
            {
                audit.Status = AuditStatus.Failed;
                audit.ErrorMessage = $"Failed to establish HTTPS connection: {ex.Message}";
                audit.CompletedAtUtc = DateTime.UtcNow;
                return audit;
            }

            audit.TtfbMs = sw.Elapsed.TotalMilliseconds;

            var html = await response.Content.ReadAsStringAsync(ct);
            sw.Stop();

            audit.HtmlSizeBytes = html.Length;

            // 4. Technology Stack Detection
            var detectedTech = TechStackDetector.Detect(html, response.Headers, response.Content.Headers);
            audit.DetectedTechStack = detectedTech.Count > 0 ? string.Join(", ", detectedTech) : "Custom HTML/CSS";

            // 5. Server Headers & Security Analysis
            AnalyzeSecurityHeaders(response.Headers, audit);

            // 6. Deep SEO, HTML & Content Signals Analysis
            AnalyzeHtmlMetadata(html, audit, uri);

            // 7. Robots.txt and Sitemap.xml Check
            audit.HasRobotsTxt = await ProbeFileExistsAsync(new Uri(httpsUri, "/robots.txt"), ct);
            audit.HasSitemapXml = await ProbeFileExistsAsync(new Uri(httpsUri, "/sitemap.xml"), ct);

            if (!audit.HasRobotsTxt)
            {
                audit.Findings.Add(new WebsiteAuditFinding
                {
                    AuditId = audit.Id,
                    Category = AuditCategory.Seo,
                    Severity = AuditSeverity.Low,
                    RuleCode = "SEO_NO_ROBOTS",
                    Title = "Missing robots.txt Configuration",
                    Description = "The website does not publish a robots.txt file to guide search engine crawlers.",
                    RecommendedFix = "Deploy a standard robots.txt file with directives and sitemap reference.",
                    SuggestedServiceId = "svc-web-dev"
                });
            }

            if (!audit.HasSitemapXml)
            {
                audit.Findings.Add(new WebsiteAuditFinding
                {
                    AuditId = audit.Id,
                    Category = AuditCategory.Seo,
                    Severity = AuditSeverity.Low,
                    RuleCode = "SEO_NO_SITEMAP",
                    Title = "Missing XML Sitemap",
                    Description = "No sitemap.xml was detected at root, which can delay indexing of new pages.",
                    RecommendedFix = "Generate and publish an automated sitemap.xml in Google Search Console.",
                    SuggestedServiceId = "svc-web-dev"
                });
            }

            // 8. Performance Checks & Compression Inspection
            AnalyzePerformance(audit, response.Headers, response.Content.Headers);

            // 9. Calculate Overall & Domain Scores
            CalculateAuditScores(audit);

            audit.Status = AuditStatus.Completed;
            audit.CompletedAtUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            audit.Status = AuditStatus.Failed;
            audit.ErrorMessage = ex.Message;
            audit.CompletedAtUtc = DateTime.UtcNow;
        }

        return audit;
    }

    public Task<List<WebsiteAudit>> GetAuditsForBusinessAsync(string businessId, CancellationToken ct = default)
    {
        return Task.FromResult(new List<WebsiteAudit>());
    }

    private async Task<bool> CheckHttpToHttpsRedirectAsync(Uri httpUri, CancellationToken ct)
    {
        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
            var resp = await client.GetAsync(httpUri, ct);

            if ((int)resp.StatusCode >= 300 && (int)resp.StatusCode <= 308)
            {
                var loc = resp.Headers.Location?.ToString();
                return loc != null && loc.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private async Task<(bool IsValid, string? Issuer, int? DaysUntilExpiry)> InspectSslCertificateAsync(string host)
    {
        try
        {
            using var tcpClient = new System.Net.Sockets.TcpClient();
            await tcpClient.ConnectAsync(host, 443);

            using var sslStream = new SslStream(tcpClient.GetStream(), false, (sender, cert, chain, errors) => true);
            await sslStream.AuthenticateAsClientAsync(host);

            if (sslStream.RemoteCertificate is X509Certificate2 cert2)
            {
                var issuer = cert2.Issuer;
                var notAfter = cert2.NotAfter;
                var daysLeft = (int)(notAfter - DateTime.UtcNow).TotalDays;
                var isValid = daysLeft > 0;

                return (isValid, issuer, daysLeft);
            }
        }
        catch
        {
            // If SSL handshake failed
        }

        return (false, null, null);
    }

    private static void AnalyzeSecurityHeaders(System.Net.Http.Headers.HttpResponseHeaders headers, WebsiteAudit audit)
    {
        int configuredCount = 0;

        // Server Banner
        if (headers.Contains("Server"))
        {
            audit.ServerBanner = string.Join(", ", headers.GetValues("Server"));
        }
        else
        {
            audit.ServerBanner = "Protected / Hidden";
        }

        // Information Disclosure: X-Powered-By
        if (headers.Contains("X-Powered-By"))
        {
            var poweredBy = string.Join(", ", headers.GetValues("X-Powered-By"));
            audit.Findings.Add(new WebsiteAuditFinding
            {
                AuditId = audit.Id,
                Category = AuditCategory.Security,
                Severity = AuditSeverity.Medium,
                RuleCode = "SEC_EXPOSED_POWERED_BY",
                Title = "Exposed Backend Technology (X-Powered-By)",
                Description = $"The server exposes internal technology signature '{poweredBy}', aiding targeted attacker profiling.",
                RecommendedFix = "Remove the 'X-Powered-By' header in php.ini / web server configuration.",
                BusinessImpact = "Allows automated bot scanners to tailor exploit payloads to specific framework versions.",
                SuggestedServiceId = "svc-security-retainer"
            });
        }

        // HSTS
        if (headers.Contains("Strict-Transport-Security"))
        {
            audit.HstsConfigured = true;
            configuredCount++;
        }
        else
        {
            audit.HstsConfigured = false;
            audit.Findings.Add(new WebsiteAuditFinding
            {
                AuditId = audit.Id,
                Category = AuditCategory.Security,
                Severity = AuditSeverity.High,
                RuleCode = "SEC_NO_HSTS",
                Title = "Missing HTTP Strict Transport Security (HSTS)",
                Description = "HSTS header is absent, making users vulnerable to SSL-stripping and man-in-the-middle attacks.",
                RecommendedFix = "Configure 'Strict-Transport-Security: max-age=31536000; includeSubDomains' in web server config.",
                BusinessImpact = "Exposes customer session tokens to network interception.",
                SuggestedServiceId = "svc-security-retainer"
            });
        }

        // CSP
        if (headers.Contains("Content-Security-Policy"))
        {
            audit.CspConfigured = true;
            configuredCount++;
        }
        else
        {
            audit.CspConfigured = false;
            audit.Findings.Add(new WebsiteAuditFinding
            {
                AuditId = audit.Id,
                Category = AuditCategory.Security,
                Severity = AuditSeverity.Medium,
                RuleCode = "SEC_NO_CSP",
                Title = "Missing Content Security Policy (CSP)",
                Description = "CSP header is not configured, which allows malicious scripts to execute in case of XSS.",
                RecommendedFix = "Implement a Content-Security-Policy restricting script-src and object-src.",
                SuggestedServiceId = "svc-security-retainer"
            });
        }

        // X-Frame-Options
        if (headers.Contains("X-Frame-Options"))
        {
            audit.XFrameOptionsConfigured = true;
            configuredCount++;
        }
        else
        {
            audit.XFrameOptionsConfigured = false;
            audit.Findings.Add(new WebsiteAuditFinding
            {
                AuditId = audit.Id,
                Category = AuditCategory.Security,
                Severity = AuditSeverity.Medium,
                RuleCode = "SEC_NO_XFRAME",
                Title = "Missing Clickjacking Protection (X-Frame-Options)",
                Description = "The page can be framed by arbitrary third-party domains, enabling clickjacking attacks.",
                RecommendedFix = "Set 'X-Frame-Options: SAMEORIGIN' or DENY.",
                SuggestedServiceId = "svc-security-retainer"
            });
        }

        // X-Content-Type-Options
        if (headers.Contains("X-Content-Type-Options"))
        {
            configuredCount++;
        }
        else
        {
            audit.Findings.Add(new WebsiteAuditFinding
            {
                AuditId = audit.Id,
                Category = AuditCategory.Security,
                Severity = AuditSeverity.Low,
                RuleCode = "SEC_NO_NOSNIFF",
                Title = "Missing X-Content-Type-Options Header",
                Description = "Browser MIME sniffing is not disabled, potentially allowing executable script execution disguised as images.",
                RecommendedFix = "Add 'X-Content-Type-Options: nosniff'.",
                SuggestedServiceId = "svc-security-retainer"
            });
        }

        // Referrer-Policy
        if (headers.Contains("Referrer-Policy"))
        {
            configuredCount++;
        }

        // Security Grade
        audit.SecurityHeadersGrade = configuredCount switch
        {
            >= 5 => "A+",
            4 => "A",
            3 => "B",
            2 => "C",
            _ => "F"
        };
    }

    private static void AnalyzeHtmlMetadata(string html, WebsiteAudit audit, Uri baseUri)
    {
        // 1. Title
        var titleMatch = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (titleMatch.Success && !string.IsNullOrWhiteSpace(titleMatch.Groups[1].Value))
        {
            audit.PageTitle = WebUtility.HtmlDecode(titleMatch.Groups[1].Value.Trim());
            if (audit.PageTitle.Length < 15)
            {
                audit.Findings.Add(new WebsiteAuditFinding
                {
                    AuditId = audit.Id,
                    Category = AuditCategory.Seo,
                    Severity = AuditSeverity.Low,
                    RuleCode = "SEO_TITLE_SHORT",
                    Title = "Page Title is Too Short",
                    Description = $"The homepage title is only {audit.PageTitle.Length} characters. Optimal length is 40-60 characters.",
                    RecommendedFix = "Expand the title tag to include business category and target location keywords."
                });
            }
            else if (audit.PageTitle.Length > 70)
            {
                audit.Findings.Add(new WebsiteAuditFinding
                {
                    AuditId = audit.Id,
                    Category = AuditCategory.Seo,
                    Severity = AuditSeverity.Low,
                    RuleCode = "SEO_TITLE_LONG",
                    Title = "Page Title is Truncated in SERP",
                    Description = $"The title tag is {audit.PageTitle.Length} characters long, which Google truncates with ellipses.",
                    RecommendedFix = "Keep the primary title tag under 60 characters for crisp SERP display."
                });
            }
        }
        else
        {
            audit.Findings.Add(new WebsiteAuditFinding
            {
                AuditId = audit.Id,
                Category = AuditCategory.Seo,
                Severity = AuditSeverity.High,
                RuleCode = "SEO_NO_TITLE",
                Title = "Missing HTML Page Title",
                Description = "Homepage is missing a <title> tag, causing severely degraded search ranking in Google.",
                RecommendedFix = "Add a descriptive <title> tag with brand name and primary services.",
                SuggestedServiceId = "svc-web-dev"
            });
        }

        // 2. Meta Description
        var descMatch = Regex.Match(html, @"<meta[^>]+name=[""']description[""'][^>]+content=[""'](.*?)[""']", RegexOptions.IgnoreCase);
        if (descMatch.Success && !string.IsNullOrWhiteSpace(descMatch.Groups[1].Value))
        {
            audit.MetaDescription = WebUtility.HtmlDecode(descMatch.Groups[1].Value.Trim());
        }
        else
        {
            audit.Findings.Add(new WebsiteAuditFinding
            {
                AuditId = audit.Id,
                Category = AuditCategory.Seo,
                Severity = AuditSeverity.Medium,
                RuleCode = "SEO_NO_META_DESC",
                Title = "Missing Meta Description Tag",
                Description = "Google search results will generate arbitrary text snippets rather than a tailored business pitch.",
                RecommendedFix = "Provide an engaging 120-160 character meta description highlighting value proposition.",
                SuggestedServiceId = "svc-web-dev"
            });
        }

        // 3. Canonical URL
        var canonicalMatch = Regex.Match(html, @"<link[^>]+rel=[""']canonical[""'][^>]+href=[""'](.*?)[""']", RegexOptions.IgnoreCase);
        if (canonicalMatch.Success)
        {
            audit.CanonicalUrl = canonicalMatch.Groups[1].Value.Trim();
        }
        else
        {
            audit.Findings.Add(new WebsiteAuditFinding
            {
                AuditId = audit.Id,
                Category = AuditCategory.Seo,
                Severity = AuditSeverity.Low,
                RuleCode = "SEO_NO_CANONICAL",
                Title = "Missing Canonical Tag",
                Description = "No canonical tag found, potentially leading to duplicate content penalties across HTTP/HTTPS or www/non-www variations.",
                RecommendedFix = $"Add '<link rel=\"canonical\" href=\"https://{baseUri.Host}/\" />'."
            });
        }

        // 4. Viewport / Mobile Friendly
        audit.ViewportConfigured = Regex.IsMatch(html, @"<meta[^>]+name=[""']viewport[""']", RegexOptions.IgnoreCase);
        if (!audit.ViewportConfigured)
        {
            audit.Findings.Add(new WebsiteAuditFinding
            {
                AuditId = audit.Id,
                Category = AuditCategory.Performance,
                Severity = AuditSeverity.Critical,
                RuleCode = "MOB_NO_VIEWPORT",
                Title = "Website is Not Mobile Optimized",
                Description = "Missing '<meta name=\"viewport\">' tag causes mobile devices to render a desktop view with tiny unreadable text.",
                RecommendedFix = "Add '<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">' and responsive CSS.",
                BusinessImpact = "Over 70% of local traffic comes from smartphones; mobile bounce rate will be extremely high.",
                SuggestedServiceId = "svc-web-dev"
            });
        }

        // 5. OpenGraph & Social Cards
        audit.OpenGraphConfigured = Regex.IsMatch(html, @"<meta[^>]+property=[""']og:", RegexOptions.IgnoreCase);
        audit.TwitterCardConfigured = Regex.IsMatch(html, @"<meta[^>]+name=[""']twitter:", RegexOptions.IgnoreCase);

        if (!audit.OpenGraphConfigured)
        {
            audit.Findings.Add(new WebsiteAuditFinding
            {
                AuditId = audit.Id,
                Category = AuditCategory.Seo,
                Severity = AuditSeverity.Low,
                RuleCode = "SEO_NO_OPENGRAPH",
                Title = "Missing OpenGraph Social Tags",
                Description = "When shared on WhatsApp, LinkedIn, or Facebook, the link generates a plain text snippet without brand logo or preview.",
                RecommendedFix = "Add og:title, og:description, and og:image tags for rich preview cards."
            });
        }

        // 6. Heading Structure (H1 / H2)
        var h1Matches = Regex.Matches(html, @"<h1[^>]*>(.*?)</h1>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        audit.H1Count = h1Matches.Count;

        if (audit.H1Count == 0)
        {
            audit.Findings.Add(new WebsiteAuditFinding
            {
                AuditId = audit.Id,
                Category = AuditCategory.Seo,
                Severity = AuditSeverity.Medium,
                RuleCode = "SEO_NO_H1",
                Title = "Missing Primary <h1> Heading",
                Description = "No <h1> heading found on homepage to signal the primary topic of the page.",
                RecommendedFix = "Add a single, clear <h1> tag introducing the business offering.",
                SuggestedServiceId = "svc-web-dev"
            });
        }
        else if (audit.H1Count > 1)
        {
            audit.Findings.Add(new WebsiteAuditFinding
            {
                AuditId = audit.Id,
                Category = AuditCategory.Seo,
                Severity = AuditSeverity.Low,
                RuleCode = "SEO_MULTIPLE_H1",
                Title = $"Multiple <h1> Tags Detected ({audit.H1Count})",
                Description = "Search engines prioritize a single distinct <h1> tag per page. Multiple H1s dilute keyword authority.",
                RecommendedFix = "Retain one primary <h1> tag and convert secondary titles to <h2>."
            });
            audit.H1Text = WebUtility.HtmlDecode(Regex.Replace(h1Matches[0].Groups[1].Value, "<.*?>", string.Empty).Trim());
        }
        else
        {
            audit.H1Text = WebUtility.HtmlDecode(Regex.Replace(h1Matches[0].Groups[1].Value, "<.*?>", string.Empty).Trim());
        }

        var h2Matches = Regex.Matches(html, @"<h2[^>]*>", RegexOptions.IgnoreCase);
        audit.H2Count = h2Matches.Count;

        // 7. Image Optimization & Accessibility
        var imgMatches = Regex.Matches(html, @"<img\b[^>]*>", RegexOptions.IgnoreCase);
        audit.ImagesTotalCount = imgMatches.Count;
        int missingAlt = 0;
        foreach (Match img in imgMatches)
        {
            var tag = img.Value;
            if (!Regex.IsMatch(tag, @"\balt=[""'][^""']*[""']", RegexOptions.IgnoreCase))
            {
                missingAlt++;
            }
        }
        audit.ImagesMissingAltCount = missingAlt;

        if (missingAlt > 0)
        {
            audit.Findings.Add(new WebsiteAuditFinding
            {
                AuditId = audit.Id,
                Category = AuditCategory.Accessibility,
                Severity = AuditSeverity.Medium,
                RuleCode = "A11Y_MISSING_IMG_ALT",
                Title = $"Missing Image Alt Text ({missingAlt}/{audit.ImagesTotalCount} images)",
                Description = $"{missingAlt} image elements lack descriptive alt attributes, impairing accessibility for visually impaired users and image SEO indexing.",
                RecommendedFix = "Provide descriptive, contextual alt attributes for all informational images.",
                SuggestedServiceId = "svc-web-dev"
            });
        }

        // 8. Link Architecture (Internal vs External)
        var linkMatches = Regex.Matches(html, @"<a\b[^>]*\bhref=[""'](.*?)[""']", RegexOptions.IgnoreCase);
        int internalLinks = 0;
        int externalLinks = 0;
        foreach (Match link in linkMatches)
        {
            var href = link.Groups[1].Value.Trim();
            if (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || href.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (href.Contains(baseUri.Host, StringComparison.OrdinalIgnoreCase)) internalLinks++;
                else externalLinks++;
            }
            else if (!href.StartsWith("#") && !href.StartsWith("javascript:") && !href.StartsWith("mailto:") && !href.StartsWith("tel:"))
            {
                internalLinks++;
            }
        }
        audit.InternalLinksCount = internalLinks;
        audit.ExternalLinksCount = externalLinks;

        // 9. Analytics & Digital Marketing Trackers Detection
        var trackers = new List<string>();
        if (Regex.IsMatch(html, @"\b(G-[A-Z0-9]+|gtag\s*\(\s*['""]config['""])\b", RegexOptions.IgnoreCase)) trackers.Add("Google Analytics 4 (GA4)");
        if (Regex.IsMatch(html, @"\bGTM-[A-Z0-9]+\b", RegexOptions.IgnoreCase)) trackers.Add("Google Tag Manager");
        if (Regex.IsMatch(html, @"\b(fbq\s*\(|connect\.facebook\.net)\b", RegexOptions.IgnoreCase)) trackers.Add("Meta Pixel");
        if (Regex.IsMatch(html, @"\b(hotjar\.com|_hjSettings)\b", RegexOptions.IgnoreCase)) trackers.Add("Hotjar Behavior Analytics");
        if (Regex.IsMatch(html, @"\banalytics\.tiktok\.com\b", RegexOptions.IgnoreCase)) trackers.Add("TikTok Pixel");
        if (Regex.IsMatch(html, @"\b(clarity\.ms|msClarity)\b", RegexOptions.IgnoreCase)) trackers.Add("Microsoft Clarity");

        audit.AnalyticsTrackersDetected = trackers.Count > 0 ? string.Join(", ", trackers) : "None Detected";

        if (trackers.Count == 0)
        {
            audit.Findings.Add(new WebsiteAuditFinding
            {
                AuditId = audit.Id,
                Category = AuditCategory.Seo,
                Severity = AuditSeverity.Medium,
                RuleCode = "MKT_NO_ANALYTICS",
                Title = "No Analytics or Conversion Trackers Installed",
                Description = "No web analytics (Google Analytics, GTM, Meta Pixel) found. Business cannot track visitor conversions or ROI.",
                RecommendedFix = "Implement Google Tag Manager and GA4 with custom conversion events.",
                SuggestedServiceId = "svc-custom-software"
            });
        }
    }

    private static void AnalyzePerformance(
        WebsiteAudit audit,
        System.Net.Http.Headers.HttpResponseHeaders respHeaders,
        System.Net.Http.Headers.HttpContentHeaders? contentHeaders)
    {
        // 1. TTFB
        if (audit.TtfbMs.HasValue)
        {
            if (audit.TtfbMs.Value > 1800)
            {
                audit.Findings.Add(new WebsiteAuditFinding
                {
                    AuditId = audit.Id,
                    Category = AuditCategory.Performance,
                    Severity = AuditSeverity.High,
                    RuleCode = "PERF_SLOW_TTFB",
                    Title = "Slow Server Response Time (TTFB)",
                    Description = $"Server took {audit.TtfbMs.Value:F0}ms to return initial HTML byte. Target is under 600ms.",
                    RecommendedFix = "Implement page caching, opcode cache, and upgrade hosting server CPU/memory.",
                    SuggestedServiceId = "svc-core-web-vitals"
                });
            }
            else if (audit.TtfbMs.Value > 1000)
            {
                audit.Findings.Add(new WebsiteAuditFinding
                {
                    AuditId = audit.Id,
                    Category = AuditCategory.Performance,
                    Severity = AuditSeverity.Medium,
                    RuleCode = "PERF_MODERATE_TTFB",
                    Title = "Moderate Server Response Delay",
                    Description = $"Time to first byte is {audit.TtfbMs.Value:F0}ms, which may cause user drop-off on 4G networks.",
                    RecommendedFix = "Enable server-side caching and optimize database queries.",
                    SuggestedServiceId = "svc-core-web-vitals"
                });
            }
        }

        // 2. HTML Size
        if (audit.HtmlSizeBytes.HasValue && audit.HtmlSizeBytes.Value > 1_500_000)
        {
            audit.Findings.Add(new WebsiteAuditFinding
            {
                AuditId = audit.Id,
                Category = AuditCategory.Performance,
                Severity = AuditSeverity.Medium,
                RuleCode = "PERF_LARGE_DOM",
                Title = "Bloated HTML Document Size",
                Description = $"HTML page size is {audit.HtmlSizeBytes.Value / 1024:N0} KB, indicating inline scripts or heavy DOM trees.",
                RecommendedFix = "Minify HTML and defer non-critical CSS/JavaScript assets.",
                SuggestedServiceId = "svc-core-web-vitals"
            });
        }

        // 3. Compression Inspection
        string encoding = "None (Uncompressed)";
        if (contentHeaders?.ContentEncoding != null && contentHeaders.ContentEncoding.Count > 0)
        {
            encoding = string.Join(", ", contentHeaders.ContentEncoding);
        }
        else if (contentHeaders != null && contentHeaders.TryGetValues("Content-Encoding", out var encValues))
        {
            encoding = string.Join(", ", encValues);
        }

        audit.CompressionType = encoding;

        if (encoding.Equals("None (Uncompressed)", StringComparison.OrdinalIgnoreCase))
        {
            audit.Findings.Add(new WebsiteAuditFinding
            {
                AuditId = audit.Id,
                Category = AuditCategory.Performance,
                Severity = AuditSeverity.Medium,
                RuleCode = "PERF_NO_COMPRESSION",
                Title = "Gzip / Brotli Compression Not Enabled",
                Description = "Web server transmits uncompressed text assets, consuming 3x more bandwidth and increasing page load time.",
                RecommendedFix = "Enable Brotli (`br`) or Gzip compression in Nginx/Apache configuration.",
                SuggestedServiceId = "svc-core-web-vitals"
            });
        }
    }

    private static void CalculateAuditScores(WebsiteAudit audit)
    {
        // Security Score (out of 100)
        int security = 100;
        if (!audit.SslValid) security -= 50;
        if (!audit.HttpRedirectsToHttps) security -= 15;
        if (audit.Findings.Any(f => f.RuleCode == "SEC_NO_HSTS")) security -= 15;
        if (audit.Findings.Any(f => f.RuleCode == "SEC_NO_CSP")) security -= 10;
        if (audit.Findings.Any(f => f.RuleCode == "SEC_NO_XFRAME")) security -= 10;
        if (audit.Findings.Any(f => f.RuleCode == "SEC_EXPOSED_POWERED_BY")) security -= 5;
        audit.SecurityScore = Math.Max(0, Math.Min(100, security));

        // SEO Score (out of 100)
        int seo = 100;
        if (audit.Findings.Any(f => f.RuleCode == "SEO_NO_TITLE")) seo -= 30;
        if (audit.Findings.Any(f => f.RuleCode == "SEO_NO_META_DESC")) seo -= 20;
        if (audit.Findings.Any(f => f.RuleCode == "MOB_NO_VIEWPORT")) seo -= 25;
        if (audit.Findings.Any(f => f.RuleCode == "SEO_NO_H1")) seo -= 15;
        if (audit.Findings.Any(f => f.RuleCode == "SEO_NO_CANONICAL")) seo -= 10;
        if (audit.Findings.Any(f => f.RuleCode == "SEO_NO_OPENGRAPH")) seo -= 5;
        if (audit.Findings.Any(f => f.RuleCode == "SEO_MULTIPLE_H1")) seo -= 5;
        if (audit.Findings.Any(f => f.RuleCode == "MKT_NO_ANALYTICS")) seo -= 10;
        if (!audit.HasRobotsTxt) seo -= 5;
        if (!audit.HasSitemapXml) seo -= 5;
        audit.SeoScore = Math.Max(0, Math.Min(100, seo));

        // Performance Score (out of 100)
        int perf = 100;
        if (audit.TtfbMs.HasValue)
        {
            if (audit.TtfbMs.Value > 2000) perf -= 40;
            else if (audit.TtfbMs.Value > 1200) perf -= 25;
            else if (audit.TtfbMs.Value > 700) perf -= 10;
        }
        if (audit.HtmlSizeBytes.HasValue && audit.HtmlSizeBytes.Value > 1_500_000) perf -= 20;
        if (audit.Findings.Any(f => f.RuleCode == "PERF_NO_COMPRESSION")) perf -= 15;
        audit.PerformanceScore = Math.Max(0, Math.Min(100, perf));

        // Accessibility (out of 100)
        int a11y = 100;
        if (audit.ImagesMissingAltCount.HasValue && audit.ImagesMissingAltCount.Value > 0)
        {
            a11y -= Math.Min(35, audit.ImagesMissingAltCount.Value * 5);
        }
        if (audit.Findings.Any(f => f.RuleCode == "MOB_NO_VIEWPORT")) a11y -= 25;
        audit.AccessibilityScore = Math.Max(0, Math.Min(100, a11y));

        // Overall Score (Weighted: 30% Perf, 30% Security, 25% SEO, 15% Accessibility)
        audit.OverallScore = (int)((audit.PerformanceScore.Value * 0.30) +
                                   (audit.SecurityScore.Value * 0.30) +
                                   (audit.SeoScore.Value * 0.25) +
                                   (audit.AccessibilityScore.Value * 0.15));
    }

    private async Task<bool> ProbeFileExistsAsync(Uri fileUri, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, fileUri);
            var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
