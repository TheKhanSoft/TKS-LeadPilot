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
            sb.AppendLine($"# Website Intelligence Audit Report: {SelectedBusiness?.Name ?? TargetUrl}");
            sb.AppendLine($"**Target URL:** {CurrentAudit.TargetUrl}");
            sb.AppendLine($"**Date Generated:** {CurrentAudit.CreatedAtUtc.ToLocalTime():g}");
            sb.AppendLine($"**Overall Score:** {CurrentAudit.OverallScore}%");
            sb.AppendLine($"**Performance:** {CurrentAudit.PerformanceScore}% | **Security:** {CurrentAudit.SecurityScore}% | **SEO:** {CurrentAudit.SeoScore}%");
            sb.AppendLine($"**Detected Technology Stack:** {CurrentAudit.DetectedTechStack ?? "Custom"}");
            sb.AppendLine($"**SSL Status:** {(CurrentAudit.SslValid ? "Valid" : "Invalid/Missing")} (Expires in {CurrentAudit.SslDaysUntilExpiry} days)");
            sb.AppendLine($"**TTFB:** {CurrentAudit.TtfbMs:F0}ms | **HTML Size:** {CurrentAudit.HtmlSizeBytes / 1024:N0} KB");
            sb.AppendLine();
            sb.AppendLine("## Audit Findings & Opportunities");
            sb.AppendLine();

            foreach (var f in CurrentAudit.Findings)
            {
                sb.AppendLine($"### [{f.Severity}] {f.Title}");
                sb.AppendLine($"- **Category:** {f.Category}");
                sb.AppendLine($"- **Description:** {f.Description}");
                if (!string.IsNullOrEmpty(f.RecommendedFix)) sb.AppendLine($"- **Recommended Fix:** {f.RecommendedFix}");
                if (!string.IsNullOrEmpty(f.BusinessImpact)) sb.AppendLine($"- **Business Impact:** {f.BusinessImpact}");
                sb.AppendLine();
            }

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
