using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.UI.ViewModels;

public partial class AuditsViewModel : ViewModelBase
{
    private readonly IBusinessRepository _businessRepository;
    private readonly IWebsiteAuditEngine _auditEngine;
    private readonly IAuditRepository _auditRepository;
    private readonly IAiProposalSynthesizer _aiSynthesizer;
    private readonly IAuditComparisonService _comparisonService;

    [ObservableProperty]
    private ObservableCollection<Business> _businessesWithWebsites = [];

    [ObservableProperty]
    private Business? _selectedBusiness;

    [ObservableProperty]
    private string _targetUrl = string.Empty;

    [ObservableProperty]
    private WebsiteAudit? _currentAudit;

    [ObservableProperty]
    private ObservableCollection<WebsiteAuditFinding> _findings = [];

    [ObservableProperty]
    private ObservableCollection<WebsiteAudit> _recentAudits = [];

    [ObservableProperty]
    private bool _isAuditing = false;

    [ObservableProperty]
    private string _statusMessage = "Select a business lead with a website to audit.";

    // AI Proposal Studio State (Stage 8)
    [ObservableProperty]
    private bool _isProposalModalOpen = false;

    [ObservableProperty]
    private ObservableCollection<string> _availableTones = ["Consultative (Local Consultant)", "ROI-Focused (Commercial Impact)", "Technical (Security & Standards)"];

    [ObservableProperty]
    private string _selectedTone = "Consultative (Local Consultant)";

    [ObservableProperty]
    private string _generatedProposalMarkdown = string.Empty;

    [ObservableProperty]
    private bool _isGeneratingProposal = false;

    [ObservableProperty]
    private string _aiProposalStatus = string.Empty;

    [ObservableProperty]
    private string _customInstructions = string.Empty;

    // Audit Comparison State (Stage 8)
    [ObservableProperty]
    private bool _isComparisonModalOpen = false;

    [ObservableProperty]
    private ObservableCollection<WebsiteAudit> _historicalAuditsForBusiness = [];

    [ObservableProperty]
    private WebsiteAudit? _selectedHistoricalAudit;

    [ObservableProperty]
    private AuditComparisonResult? _comparisonResult;

    [ObservableProperty]
    private string _comparisonMarkdown = string.Empty;

    public AuditsViewModel(
        IBusinessRepository businessRepository,
        IWebsiteAuditEngine auditEngine,
        IAuditRepository auditRepository,
        IAiProposalSynthesizer aiSynthesizer,
        IAuditComparisonService comparisonService)
    {
        _businessRepository = businessRepository;
        _auditEngine = auditEngine;
        _auditRepository = auditRepository;
        _aiSynthesizer = aiSynthesizer;
        _comparisonService = comparisonService;

        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        try
        {
            var all = await _businessRepository.GetAllAsync();
            var withWeb = all.Where(b => !string.IsNullOrWhiteSpace(b.WebsiteUrl)).ToList();
            BusinessesWithWebsites = new ObservableCollection<Business>(withWeb);

            SelectedBusiness = BusinessesWithWebsites.FirstOrDefault();
            if (SelectedBusiness != null)
            {
                TargetUrl = SelectedBusiness.WebsiteUrl ?? string.Empty;
                await LoadLatestAuditForBusiness(SelectedBusiness.Id);
            }

            var recent = await _auditRepository.GetRecentAuditsAsync(20);
            RecentAudits = new ObservableCollection<WebsiteAudit>(recent);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Init Error: {ex.Message}";
        }
    }

    partial void OnSelectedBusinessChanged(Business? value)
    {
        if (value != null)
        {
            TargetUrl = value.WebsiteUrl ?? string.Empty;
            _ = LoadLatestAuditForBusiness(value.Id);
        }
    }

    private async Task LoadLatestAuditForBusiness(string businessId)
    {
        try
        {
            var audit = await _auditRepository.GetLatestAuditForBusinessAsync(businessId);
            if (audit != null)
            {
                CurrentAudit = audit;
                Findings = new ObservableCollection<WebsiteAuditFinding>(audit.Findings);
                StatusMessage = $"Loaded previous audit from {audit.CreatedAtUtc.ToLocalTime():g}. Overall Score: {audit.OverallScore}%";
            }
            else
            {
                CurrentAudit = null;
                Findings.Clear();
                StatusMessage = "No prior audit exists for this business. Click 'Run Audit' to diagnose.";
            }
        }
        catch
        {
            // Fail gracefully
        }
    }

    [RelayCommand]
    public async Task RunAudit()
    {
        if (string.IsNullOrWhiteSpace(TargetUrl))
        {
            StatusMessage = "Please enter a valid website URL to audit.";
            return;
        }

        try
        {
            IsAuditing = true;
            StatusMessage = $"Running live security, SEO, and performance diagnostics on {TargetUrl}...";

            var bizId = SelectedBusiness?.Id ?? Guid.NewGuid().ToString();
            var audit = await _auditEngine.RunAuditAsync(bizId, TargetUrl);

            // Save to database
            await _auditRepository.SaveAuditAsync(audit);

            CurrentAudit = audit;
            Findings = new ObservableCollection<WebsiteAuditFinding>(audit.Findings);

            var recent = await _auditRepository.GetRecentAuditsAsync(20);
            RecentAudits = new ObservableCollection<WebsiteAudit>(recent);

            if (audit.Status == Core.Enums.AuditStatus.Completed)
            {
                StatusMessage = $"Audit complete! Overall Score: {audit.OverallScore}%. Found {audit.Findings.Count} potential issues/opportunities.";
            }
            else
            {
                StatusMessage = $"Audit failed: {audit.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Audit Error: {ex.Message}";
        }
        finally
        {
            IsAuditing = false;
        }
    }

    [RelayCommand]
    public async Task ExportReport()
    {
        if (CurrentAudit == null) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# TKS Website Intelligence & Deep Technical Audit Report");
            sb.AppendLine($"**Organization:** {SelectedBusiness?.Name ?? "Target Enterprise"}");
            sb.AppendLine($"**Target URL:** {CurrentAudit.TargetUrl}");
            sb.AppendLine($"**Audit Date:** {CurrentAudit.CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss} (Local)");
            sb.AppendLine($"**Audit Engine:** TKS LeadPilot Native Inspector v1.1.0");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## 1. Executive Summary & Composite Health Scores");
            sb.AppendLine();
            sb.AppendLine($"| Metric | Score | Diagnostic Status |");
            sb.AppendLine($"|---|:---:|---|");
            sb.AppendLine($"| **Overall Digital Health** | **{CurrentAudit.OverallScore}%** | Composite Technical Readiness Index |");
            sb.AppendLine($"| **Performance & Speed** | {CurrentAudit.PerformanceScore}% | TTFB: {CurrentAudit.TtfbMs:F0}ms • Compression: {CurrentAudit.CompressionType ?? "Unknown"} |");
            sb.AppendLine($"| **Security & Cryptography** | {CurrentAudit.SecurityScore}% | SSL: {(CurrentAudit.SslValid ? "Active Valid" : "INVALID")} • Grade: {CurrentAudit.SecurityHeadersGrade ?? "N/A"} |");
            sb.AppendLine($"| **SEO & Discoverability** | {CurrentAudit.SeoScore}% | Title: {(string.IsNullOrEmpty(CurrentAudit.PageTitle) ? "Missing" : "Configured")} • Canonical: {(string.IsNullOrEmpty(CurrentAudit.CanonicalUrl) ? "Missing" : "Active")} |");
            sb.AppendLine($"| **Accessibility & UX** | {CurrentAudit.AccessibilityScore}% | Mobile Viewport: {(CurrentAudit.ViewportConfigured ? "Yes" : "NO")} • Missing Alt: {CurrentAudit.ImagesMissingAltCount ?? 0} |");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## 2. Infrastructure & Deep Technical Forensics");
            sb.AppendLine();
            sb.AppendLine($"- **Server Banner:** `{CurrentAudit.ServerBanner ?? "Protected"}`");
            sb.AppendLine($"- **Content Compression:** `{CurrentAudit.CompressionType ?? "Uncompressed"}`");
            sb.AppendLine($"- **Detected Tech Stack & CMS:** `{CurrentAudit.DetectedTechStack ?? "Custom HTML/CSS"}`");
            sb.AppendLine($"- **SSL Certificate Issuer:** `{CurrentAudit.SslIssuer ?? "None"}` (Valid for {CurrentAudit.SslDaysUntilExpiry ?? 0} days)");
            sb.AppendLine($"- **HTTPS Auto-Redirection:** `{(CurrentAudit.HttpRedirectsToHttps ? "Enforced (301/308)" : "NOT ENFORCED - Insecure")}`");
            sb.AppendLine($"- **Time-To-First-Byte (TTFB):** `{CurrentAudit.TtfbMs:F0}ms` (Google Target: <600ms)");
            sb.AppendLine($"- **Raw HTML Document Payload:** `{CurrentAudit.HtmlSizeBytes / 1024:N0} KB`");
            sb.AppendLine($"- **robots.txt Crawler Directives:** `{(CurrentAudit.HasRobotsTxt ? "Present" : "MISSING")}`");
            sb.AppendLine($"- **XML Sitemap Index:** `{(CurrentAudit.HasSitemapXml ? "Present" : "MISSING")}`");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## 3. SEO, Social & Content Architecture");
            sb.AppendLine();
            sb.AppendLine($"- **Page Title:** `{CurrentAudit.PageTitle ?? "None"}` ({CurrentAudit.PageTitle?.Length ?? 0} chars)");
            sb.AppendLine($"- **Meta Description:** `{CurrentAudit.MetaDescription ?? "None"}` ({CurrentAudit.MetaDescription?.Length ?? 0} chars)");
            sb.AppendLine($"- **Canonical URL Tag:** `{CurrentAudit.CanonicalUrl ?? "Missing"}`");
            sb.AppendLine($"- **Primary <h1> Heading:** `{CurrentAudit.H1Text ?? "None detected"}` (Count: {CurrentAudit.H1Count ?? 0})");
            sb.AppendLine($"- **Secondary <h2> Headings Count:** `{CurrentAudit.H2Count ?? 0}`");
            sb.AppendLine($"- **Total Images Audited:** `{CurrentAudit.ImagesTotalCount ?? 0}` (Missing Alt Attributes: `{CurrentAudit.ImagesMissingAltCount ?? 0}`)");
            sb.AppendLine($"- **Hyperlink Architecture:** `{CurrentAudit.InternalLinksCount ?? 0} Internal Links` • `{CurrentAudit.ExternalLinksCount ?? 0} External Links`");
            sb.AppendLine($"- **OpenGraph Social Preview Tags:** `{(CurrentAudit.OpenGraphConfigured ? "Configured" : "MISSING")}`");
            sb.AppendLine($"- **Twitter Card Tags:** `{(CurrentAudit.TwitterCardConfigured ? "Configured" : "MISSING")}`");
            sb.AppendLine($"- **Mobile Viewport Meta Tag:** `{(CurrentAudit.ViewportConfigured ? "Mobile-Friendly" : "NON-RESPONSIVE (Desktop-only)")}`");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## 4. Security & Privacy Hygiene Matrix");
            sb.AppendLine();
            sb.AppendLine($"| Security Header | Status | Risk Level if Missing |");
            sb.AppendLine($"|---|:---:|---|");
            sb.AppendLine($"| **Strict-Transport-Security (HSTS)** | `{(CurrentAudit.HstsConfigured ? "PASS" : "FAIL")}` | High: Vulnerable to SSL-stripping & MITM |");
            sb.AppendLine($"| **Content-Security-Policy (CSP)** | `{(CurrentAudit.CspConfigured ? "PASS" : "FAIL")}` | Medium: Susceptible to Cross-Site Scripting (XSS) |");
            sb.AppendLine($"| **X-Frame-Options** | `{(CurrentAudit.XFrameOptionsConfigured ? "PASS" : "FAIL")}` | Medium: Vulnerable to Clickjacking iFrame embedding |");
            sb.AppendLine($"| **Security Headers Composite Grade** | **{CurrentAudit.SecurityHeadersGrade ?? "N/A"}** | Security posture evaluated against modern OWASP guidelines |");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## 5. Analytics & Digital Marketing Tracking");
            sb.AppendLine();
            sb.AppendLine($"- **Detected Trackers & Pixels:** `{CurrentAudit.AnalyticsTrackersDetected ?? "None Detected"}`");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## 6. Actionable Findings & Remediations");
            sb.AppendLine();

            foreach (var f in CurrentAudit.Findings)
            {
                sb.AppendLine($"### [{f.Severity}] {f.Title}");
                sb.AppendLine($"- **Domain:** {f.Category}");
                sb.AppendLine($"- **Rule Code:** `{f.RuleCode}`");
                sb.AppendLine($"- **Observation:** {f.Description}");
                if (!string.IsNullOrEmpty(f.BusinessImpact)) sb.AppendLine($"- **Commercial Impact:** {f.BusinessImpact}");
                if (!string.IsNullOrEmpty(f.RecommendedFix)) sb.AppendLine($"- **Recommended Fix:** {f.RecommendedFix}");
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine($"*Report produced by TheKhanSoft Enterprise Suite — https://thekhansoft.com*");

            var exportDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (!Directory.Exists(exportDir)) exportDir = AppContext.BaseDirectory;

            var filename = $"TKS_Audit_{SanitizeFilename(SelectedBusiness?.Name ?? "Website")}_{DateTime.Now:yyyyMMdd}.md";
            var path = Path.Combine(exportDir, filename);
            await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8);

            StatusMessage = $"Saved audit report to {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export Error: {ex.Message}";
        }
    }

    // --- AI Proposal Studio Commands (Stage 8) ---
    [RelayCommand]
    public void OpenProposalModal()
    {
        if (CurrentAudit == null)
        {
            StatusMessage = "Please run or select an audit first before generating a proposal.";
            return;
        }

        if (string.IsNullOrWhiteSpace(GeneratedProposalMarkdown))
        {
            ExecuteOfflineProposalGeneration();
        }

        IsProposalModalOpen = true;
    }

    [RelayCommand]
    public void CloseProposalModal()
    {
        IsProposalModalOpen = false;
    }

    [RelayCommand]
    public async Task GenerateAiProposal()
    {
        if (CurrentAudit == null) return;

        try
        {
            IsGeneratingProposal = true;
            AiProposalStatus = "Connecting to Google Gemini API (Free Tier) to synthesize executive proposal...";

            var tone = SelectedTone.StartsWith("ROI") ? AiTone.RoiFocused
                     : SelectedTone.StartsWith("Tech") ? AiTone.Technical
                     : AiTone.Consultative;

            var request = new AuditProposalSynthesisRequest
            {
                Business = SelectedBusiness ?? new Business { Name = "Client Lead", WebsiteUrl = TargetUrl },
                Audit = CurrentAudit,
                Tone = tone,
                CustomInstructions = CustomInstructions
            };

            var result = await _aiSynthesizer.SynthesizeProposalAsync(request);

            GeneratedProposalMarkdown = result.FullProposalMarkdown;
            if (result.IsLocalFallback)
            {
                AiProposalStatus = $"Offline Fallback Generated: {result.ErrorMessage}";
            }
            else
            {
                AiProposalStatus = $"Generated via Gemini AI ({result.Tone}). Quota used today: {result.RequestsUsedToday}/{result.DailyLimit}. Tokens: {result.TokensUsed ?? 0}.";
            }
        }
        catch (Exception ex)
        {
            AiProposalStatus = $"AI Error: {ex.Message}";
        }
        finally
        {
            IsGeneratingProposal = false;
        }
    }

    [RelayCommand]
    public void GenerateOfflineProposal()
    {
        ExecuteOfflineProposalGeneration();
    }

    private void ExecuteOfflineProposalGeneration()
    {
        if (CurrentAudit == null) return;

        var tone = SelectedTone.StartsWith("ROI") ? AiTone.RoiFocused
                 : SelectedTone.StartsWith("Tech") ? AiTone.Technical
                 : AiTone.Consultative;

        var request = new AuditProposalSynthesisRequest
        {
            Business = SelectedBusiness ?? new Business { Name = "Client Lead", WebsiteUrl = TargetUrl },
            Audit = CurrentAudit,
            Tone = tone,
            CustomInstructions = CustomInstructions
        };

        var result = _aiSynthesizer.SynthesizeDeterministicProposal(request);
        GeneratedProposalMarkdown = result.FullProposalMarkdown;
        AiProposalStatus = "Generated instant deterministic proposal template ($0 cost, 100% offline).";
    }

    [RelayCommand]
    public async Task ExportProposal()
    {
        if (string.IsNullOrWhiteSpace(GeneratedProposalMarkdown)) return;

        try
        {
            var exportDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (!Directory.Exists(exportDir)) exportDir = AppContext.BaseDirectory;

            var filename = $"TKS_Proposal_{SanitizeFilename(SelectedBusiness?.Name ?? "Client")}_{DateTime.Now:yyyyMMdd_HHmm}.md";
            var path = Path.Combine(exportDir, filename);
            await File.WriteAllTextAsync(path, GeneratedProposalMarkdown, Encoding.UTF8);

            AiProposalStatus = $"Saved proposal to {path}";
            StatusMessage = $"Saved proposal to {path}";
        }
        catch (Exception ex)
        {
            AiProposalStatus = $"Export Error: {ex.Message}";
        }
    }

    // --- Audit Comparison Commands (Stage 8) ---
    [RelayCommand]
    public async Task OpenComparisonModal()
    {
        if (SelectedBusiness == null || CurrentAudit == null)
        {
            StatusMessage = "Please select a business with completed audits to compare.";
            return;
        }

        try
        {
            var allAudits = await _auditRepository.GetAllAuditsForBusinessAsync(SelectedBusiness.Id);
            var otherAudits = allAudits.Where(a => a.Id != CurrentAudit.Id).ToList();

            if (otherAudits.Count == 0)
            {
                StatusMessage = "Only 1 audit exists for this business. Run a re-audit to compare historical changes.";
                return;
            }

            HistoricalAuditsForBusiness = new ObservableCollection<WebsiteAudit>(otherAudits);
            SelectedHistoricalAudit = HistoricalAuditsForBusiness.FirstOrDefault();
            ExecuteAuditComparison();

            IsComparisonModalOpen = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Comparison error: {ex.Message}";
        }
    }

    [RelayCommand]
    public void CloseComparisonModal()
    {
        IsComparisonModalOpen = false;
    }

    [RelayCommand]
    public void ExecuteAuditComparison()
    {
        if (CurrentAudit == null || SelectedHistoricalAudit == null) return;

        var baseline = SelectedHistoricalAudit.CreatedAtUtc < CurrentAudit.CreatedAtUtc
            ? SelectedHistoricalAudit
            : CurrentAudit;
        var target = baseline == SelectedHistoricalAudit ? CurrentAudit : SelectedHistoricalAudit;

        ComparisonResult = _comparisonService.CompareAudits(baseline, target);
        ComparisonMarkdown = ComparisonResult.ComparisonSummaryMarkdown;
    }

    private static string SanitizeFilename(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Replace(" ", "_");
    }
}
