using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Data.Services;

public partial class OutreachService : IOutreachService
{
    private readonly AppDbContext _context;

    [GeneratedRegex(@"[^\d]", RegexOptions.Compiled)]
    private static partial Regex DigitsOnlyRegex();

    public OutreachService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<OutreachTemplate>> GetTemplatesAsync(OutreachChannel? channel = null, CancellationToken ct = default)
    {
        var query = _context.OutreachTemplates.Where(t => t.IsActive);
        if (channel.HasValue)
        {
            query = query.Where(t => t.Channel == channel.Value);
        }
        return await query.ToListAsync(ct);
    }

    public async Task<OutreachMessage> CreateDraftAsync(
        string businessId,
        string templateId,
        string? contactId = null,
        CancellationToken ct = default)
    {
        var business = await _context.Businesses
            .Include(b => b.Contacts)
            .FirstOrDefaultAsync(b => b.Id == businessId, ct)
            ?? throw new ArgumentException($"Business with ID '{businessId}' not found.");

        // Compliance Guard: Suppression List
        if (IsSuppressed(business))
        {
            throw new InvalidOperationException($"Cannot create outreach draft: '{business.Name}' is on the Do-Not-Contact suppression list.");
        }

        var template = await _context.OutreachTemplates.FirstOrDefaultAsync(t => t.Id == templateId, ct)
            ?? throw new ArgumentException($"Template with ID '{templateId}' not found.");

        var contact = !string.IsNullOrWhiteSpace(contactId)
            ? business.Contacts.FirstOrDefault(c => c.Id == contactId)
            : business.Contacts.FirstOrDefault();

        var audit = await _context.WebsiteAudits
            .Include(a => a.Findings)
            .Where(a => a.BusinessId == business.Id)
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        var topIssue = audit?.Findings.OrderBy(f => f.Severity).FirstOrDefault()?.Title
            ?? "server latency and mobile responsiveness";

        var suggestedService = !string.IsNullOrWhiteSpace(business.WebsiteUrl)
            ? "Website Speed Optimization and Cloud Modernization"
            : "Modern Responsive Website Development";

        var renderedSubject = !string.IsNullOrWhiteSpace(template.SubjectTemplate)
            ? RenderTemplate(template.SubjectTemplate, business, contact, audit, suggestedService)
            : null;

        var renderedBody = RenderTemplate(template.BodyTemplate, business, contact, audit, suggestedService);

        var recipient = template.Channel == OutreachChannel.Email
            ? contact?.Email ?? business.EmailPrimary ?? string.Empty
            : contact?.Phone ?? business.PhonePrimary ?? string.Empty;

        var message = new OutreachMessage
        {
            BusinessId = business.Id,
            ContactId = contact?.Id,
            Channel = template.Channel,
            TemplateId = template.Id,
            Subject = renderedSubject,
            Body = template.BodyTemplate,
            RenderedBody = renderedBody,
            RecipientAddress = recipient,
            ApprovalStatus = OutreachApprovalStatus.PendingApproval,
            IsSent = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _context.OutreachMessages.Add(message);
        await _context.SaveChangesAsync(ct);
        return message;
    }

    public async Task<OutreachMessage> CreateCustomDraftAsync(OutreachMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var business = await _context.Businesses.FirstOrDefaultAsync(b => b.Id == message.BusinessId, ct)
            ?? throw new ArgumentException($"Business with ID '{message.BusinessId}' not found.");

        if (IsSuppressed(business))
        {
            throw new InvalidOperationException($"Cannot create outreach draft: '{business.Name}' is on the Do-Not-Contact suppression list.");
        }

        message.ApprovalStatus = OutreachApprovalStatus.PendingApproval;
        message.CreatedAtUtc = DateTime.UtcNow;
        message.UpdatedAtUtc = DateTime.UtcNow;

        _context.OutreachMessages.Add(message);
        await _context.SaveChangesAsync(ct);
        return message;
    }

    public async Task<List<OutreachMessage>> GetApprovalQueueAsync(CancellationToken ct = default)
    {
        return await _context.OutreachMessages
            .Include(m => m.Business)
            .Include(m => m.Contact)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<List<OutreachMessage>> GetMessagesForBusinessAsync(string businessId, CancellationToken ct = default)
    {
        return await _context.OutreachMessages
            .Include(m => m.Contact)
            .Where(m => m.BusinessId == businessId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<bool> ApproveMessageAsync(string messageId, CancellationToken ct = default)
    {
        var msg = await _context.OutreachMessages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (msg == null) return false;

        msg.ApprovalStatus = OutreachApprovalStatus.Approved;
        msg.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RejectMessageAsync(string messageId, CancellationToken ct = default)
    {
        var msg = await _context.OutreachMessages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (msg == null) return false;

        msg.ApprovalStatus = OutreachApprovalStatus.Rejected;
        msg.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> MarkAsSentAsync(string messageId, CancellationToken ct = default)
    {
        var msg = await _context.OutreachMessages
            .Include(m => m.Business)
            .FirstOrDefaultAsync(m => m.Id == messageId, ct);

        if (msg == null) return false;

        msg.IsSent = true;
        msg.SentAtUtc = DateTime.UtcNow;
        msg.ApprovalStatus = OutreachApprovalStatus.Approved;
        msg.UpdatedAtUtc = DateTime.UtcNow;

        // Auto transition business pipeline stage to Contacted
        if (msg.Business != null)
        {
            var contactedStage = await _context.PipelineStages.FirstOrDefaultAsync(s => s.Code == "CONTACTED", ct);
            if (contactedStage != null)
            {
                msg.Business.PipelineStageId = contactedStage.Id;
            }
            msg.Business.LeadStatus = LeadStatus.Contacted;
            msg.Business.UpdatedAtUtc = DateTime.UtcNow;

            _context.SalesNotes.Add(new SalesNote
            {
                BusinessId = msg.BusinessId,
                AuthorName = "Outreach",
                Content = $"Sent {msg.Channel} message to {msg.RecipientAddress}. Stage moved to Contacted.",
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync(ct);
        return true;
    }

    public string RenderTemplate(
        string template,
        Business business,
        BusinessContact? contact,
        WebsiteAudit? audit,
        string? serviceName = null)
    {
        if (string.IsNullOrWhiteSpace(template)) return string.Empty;

        var contactName = !string.IsNullOrWhiteSpace(contact?.FullName)
            ? contact.FullName
            : "Team";

        var topIssue = audit?.Findings.OrderBy(f => f.Severity).FirstOrDefault()?.Title
            ?? "server latency and Core Web Vitals optimization";

        var rendered = template
            .Replace("{{business_name}}", !string.IsNullOrWhiteSpace(business.Name) ? business.Name : "your organization")
            .Replace("{{contact_name}}", contactName)
            .Replace("{{category}}", !string.IsNullOrWhiteSpace(business.Category) ? business.Category : "business")
            .Replace("{{city}}", !string.IsNullOrWhiteSpace(business.City) ? business.City : "your area")
            .Replace("{{website_url}}", !string.IsNullOrWhiteSpace(business.WebsiteUrl) ? business.WebsiteUrl : "your website")
            .Replace("{{lead_score}}", $"{business.LeadScore}")
            .Replace("{{top_audit_issue}}", topIssue)
            .Replace("{{suggested_service}}", serviceName ?? "modern high-speed web solutions")
            .Replace("{{ttfb_ms}}", $"{audit?.TtfbMs ?? 1200:F0}")
            .Replace("{{performance_score}}", $"{audit?.PerformanceScore ?? 50}");

        return rendered;
    }

    public string BuildMailtoUri(OutreachMessage message)
    {
        var subject = Uri.EscapeDataString(message.Subject ?? "Outreach Inquiry");
        var body = Uri.EscapeDataString(message.RenderedBody);
        return $"mailto:{message.RecipientAddress}?subject={subject}&body={body}";
    }

    public string BuildWhatsAppUri(OutreachMessage message)
    {
        var phoneDigits = DigitsOnlyRegex().Replace(message.RecipientAddress, "");
        if (phoneDigits.StartsWith("00")) phoneDigits = phoneDigits[2..];
        if (phoneDigits.StartsWith("0")) phoneDigits = "92" + phoneDigits[1..];
        var text = Uri.EscapeDataString(message.RenderedBody);
        return $"https://wa.me/{phoneDigits}?text={text}";
    }

    private static bool IsSuppressed(Business business)
    {
        if (business.LeadStatus == LeadStatus.DoNotContact) return true;
        if (business.PipelineStageId == "stage-do-not-contact") return true;
        if (business.HasTag("DoNotContact") || business.HasTag("Suppressed")) return true;
        return false;
    }
}
