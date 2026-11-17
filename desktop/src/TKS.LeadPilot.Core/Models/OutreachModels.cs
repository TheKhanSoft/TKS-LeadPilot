using System;
using TKS.LeadPilot.Core.Enums;

namespace TKS.LeadPilot.Core.Models;

public class OutreachMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string BusinessId { get; set; } = string.Empty;
    public Business? Business { get; set; }

    public string? ContactId { get; set; }
    public BusinessContact? Contact { get; set; }

    public OutreachChannel Channel { get; set; } = OutreachChannel.Email;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public string RenderedBody { get; set; } = string.Empty;
    public string RecipientAddress { get; set; } = string.Empty;

    public OutreachApprovalStatus ApprovalStatus { get; set; } = OutreachApprovalStatus.PendingApproval;
    public bool IsSent { get; set; } = false;
    public DateTime? SentAtUtc { get; set; }

    public string? TemplateId { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class OutreachTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public OutreachChannel Channel { get; set; } = OutreachChannel.Email;
    public string Category { get; set; } = "General";
    public string? SubjectTemplate { get; set; }
    public string BodyTemplate { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
