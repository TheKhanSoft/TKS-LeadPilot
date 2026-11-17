using System;
using System.Collections.Generic;

namespace TKS.LeadPilot.Core.Models;

/// <summary>
/// Client synchronization settings and endpoint parameters.
/// </summary>
public class SyncConfig
{
    public string ApiBaseUrl { get; set; } = "http://127.0.0.1:8000/api/v1";
    public string? ApiToken { get; set; }
    public string DeviceId { get; set; } = Guid.NewGuid().ToString();
    public string DeviceName { get; set; } = Environment.MachineName;
    public string Platform { get; set; } = "windows";
    public DateTime? LastSyncedAtUtc { get; set; }
    public bool AutoSyncEnabled { get; set; } = false;
    public int SyncIntervalMinutes { get; set; } = 15;
}

/// <summary>
/// Overview of synchronization connectivity and pending queue status.
/// </summary>
public class SyncStatusResult
{
    public bool IsConfigured { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }
    public int PendingLocalChangesCount { get; set; }
    public string? ServerMessage { get; set; }
    public string? ServerVersion { get; set; }
}

/// <summary>
/// Payload pushed by client to central Laravel API containing changed local records.
/// </summary>
public class SyncPushPayload
{
    public string DeviceId { get; set; } = string.Empty;
    public DateTime PushedAtUtc { get; set; } = DateTime.UtcNow;
    public List<Business> Businesses { get; set; } = [];
    public List<BusinessContact> Contacts { get; set; } = [];
    public List<WebsiteAudit> WebsiteAudits { get; set; } = [];
    public List<PipelineStage> PipelineStages { get; set; } = [];
    public List<SalesTask> SalesTasks { get; set; } = [];
    public List<SalesNote> SalesNotes { get; set; } = [];
    public List<OutreachMessage> OutreachMessages { get; set; } = [];
}

/// <summary>
/// Result returned after pushing local changes to central server.
/// </summary>
public class SyncPushResult
{
    public bool Success { get; set; }
    public int PushedCount { get; set; }
    public int ConflictsCount { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Payload returned by central Laravel API containing delta updates since client's last sync.
/// </summary>
public class SyncPullResponse
{
    public DateTime ServerTimestamp { get; set; } = DateTime.UtcNow;
    public List<Business> Businesses { get; set; } = [];
    public List<BusinessContact> Contacts { get; set; } = [];
    public List<WebsiteAudit> WebsiteAudits { get; set; } = [];
    public List<PipelineStage> PipelineStages { get; set; } = [];
    public List<SalesTask> SalesTasks { get; set; } = [];
    public List<SalesNote> SalesNotes { get; set; } = [];
    public List<OutreachMessage> OutreachMessages { get; set; } = [];
}

/// <summary>
/// Result returned after pulling and integrating remote changes into local SQLite.
/// </summary>
public class SyncPullResult
{
    public bool Success { get; set; }
    public int PulledCount { get; set; }
    public int AppliedCount { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Full bidirectional synchronization result.
/// </summary>
public class FullSyncResult
{
    public bool Success { get; set; }
    public int PushedCount { get; set; }
    public int PulledCount { get; set; }
    public int ConflictsDetected { get; set; }
    public DateTime SyncedAtUtc { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Conflict record created when client and server revisions diverge.
/// </summary>
public class SyncConflictRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public int ClientVersion { get; set; }
    public int ServerVersion { get; set; }
    public string Resolution { get; set; } = "ServerWins";
    public DateTime DetectedAtUtc { get; set; } = DateTime.UtcNow;
}
