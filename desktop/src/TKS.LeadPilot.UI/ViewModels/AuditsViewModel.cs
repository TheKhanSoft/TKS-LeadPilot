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

    public AuditsViewModel(
        IBusinessRepository businessRepository,
        IWebsiteAuditEngine auditEngine,
        IAuditRepository auditRepository)
    {
        _businessRepository = businessRepository;
        _auditEngine = auditEngine;
        _auditRepository = auditRepository;

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

    private static string SanitizeFilename(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Replace(" ", "_");
    }
}
