namespace TKS.LeadPilot.Core.Enums;

public enum LeadStatus
{
    Discovered,
    ToReview,
    Qualified,
    Researching,
    ContactDrafted,
    AwaitingApproval,
    Contacted,
    Responded,
    MeetingScheduled,
    ProposalSent,
    Negotiation,
    Won,
    Lost,
    DoNotContact
}

public enum WebsiteStatus
{
    ActiveWebsite,
    NoWebsite,
    ParkedDomain,
    UnderConstruction,
    ConnectionError,
    BlockedSsrf
}

public enum AuditSeverity
{
    Critical,
    High,
    Medium,
    Low,
    Informational
}

public enum AuditCategory
{
    Performance,
    Seo,
    Accessibility,
    Security,
    Conversion
}

public enum AuditStatus
{
    Queued,
    Running,
    Completed,
    Failed
}

public enum OutreachChannel
{
    Email,
    WhatsApp
}

public enum OutreachApprovalStatus
{
    PendingApproval,
    Approved,
    Rejected
}

public enum TaskPriority
{
    Low,
    Medium,
    High,
    Urgent
}
