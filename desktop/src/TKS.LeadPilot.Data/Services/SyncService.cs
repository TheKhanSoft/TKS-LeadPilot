using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Data.Services;

public class SyncService : ISyncService
{
    private readonly AppDbContext _context;
    private readonly ISecretVault? _secretVault;
    private readonly HttpClient _httpClient;

    private const string SettingKeySyncBaseUrl = "SYNC_BASE_URL";
    private const string SettingKeySyncDeviceId = "SYNC_DEVICE_ID";
    private const string SettingKeySyncDeviceName = "SYNC_DEVICE_NAME";
    private const string SettingKeySyncLastAt = "SYNC_LAST_AT_UTC";
    private const string VaultKeySyncToken = "SYNC_BEARER_TOKEN";

    public SyncService(AppDbContext context, ISecretVault? secretVault = null, HttpClient? httpClient = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _secretVault = secretVault;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task<SyncConfig> GetConfigAsync(CancellationToken ct = default)
    {
        var baseUrl = await GetSettingAsync(SettingKeySyncBaseUrl, "http://127.0.0.1:8000/api/v1", ct);
        var deviceId = await GetSettingAsync(SettingKeySyncDeviceId, string.Empty, ct);
        if (string.IsNullOrEmpty(deviceId))
        {
            deviceId = Guid.NewGuid().ToString();
            await SaveSettingAsync(SettingKeySyncDeviceId, deviceId, ct);
        }

        var deviceName = await GetSettingAsync(SettingKeySyncDeviceName, Environment.MachineName, ct);
        var lastSyncedStr = await GetSettingAsync(SettingKeySyncLastAt, string.Empty, ct);
        DateTime? lastSyncedUtc = DateTime.TryParse(lastSyncedStr, out var parsed) ? parsed.ToUniversalTime() : null;

        string? token = null;
        if (_secretVault != null && _secretVault.HasSecret(VaultKeySyncToken))
        {
            token = _secretVault.GetSecret(VaultKeySyncToken);
        }

        return new SyncConfig
        {
            ApiBaseUrl = baseUrl,
            DeviceId = deviceId,
            DeviceName = deviceName,
            LastSyncedAtUtc = lastSyncedUtc,
            ApiToken = token
        };
    }

    public async Task SaveConfigAsync(SyncConfig config, CancellationToken ct = default)
    {
        await SaveSettingAsync(SettingKeySyncBaseUrl, config.ApiBaseUrl, ct);
        await SaveSettingAsync(SettingKeySyncDeviceId, config.DeviceId, ct);
        await SaveSettingAsync(SettingKeySyncDeviceName, config.DeviceName, ct);

        if (config.LastSyncedAtUtc.HasValue)
        {
            await SaveSettingAsync(SettingKeySyncLastAt, config.LastSyncedAtUtc.Value.ToString("O"), ct);
        }

        if (_secretVault != null)
        {
            if (!string.IsNullOrWhiteSpace(config.ApiToken))
            {
                _secretVault.SaveSecret(VaultKeySyncToken, config.ApiToken);
            }
            else
            {
                _secretVault.DeleteSecret(VaultKeySyncToken);
            }
        }
    }

    public async Task<SyncStatusResult> GetSyncStatusAsync(CancellationToken ct = default)
    {
        var config = await GetConfigAsync(ct);
        var result = new SyncStatusResult
        {
            IsConfigured = !string.IsNullOrWhiteSpace(config.ApiBaseUrl),
            LastSyncedAtUtc = config.LastSyncedAtUtc
        };

        // Count local pending changes
        var lastSync = config.LastSyncedAtUtc ?? DateTime.MinValue;
        result.PendingLocalChangesCount = await _context.Businesses.CountAsync(b => b.UpdatedAtUtc > lastSync, ct);

        try
        {
            var healthUrl = $"{config.ApiBaseUrl.TrimEnd('/')}/health";
            var response = await _httpClient.GetAsync(healthUrl, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                result.IsOnline = true;
                result.ServerMessage = doc.RootElement.GetProperty("status").GetString();
                result.ServerVersion = doc.RootElement.GetProperty("version").GetString();
            }
        }
        catch
        {
            result.IsOnline = false;
            result.ServerMessage = "Server unreachable";
        }

        return result;
    }

    public async Task<SyncPushResult> PushLocalChangesAsync(CancellationToken ct = default)
    {
        var config = await GetConfigAsync(ct);
        var lastSync = config.LastSyncedAtUtc ?? DateTime.MinValue;

        var businesses = await _context.Businesses
            .Where(b => b.UpdatedAtUtc > lastSync)
            .Take(100)
            .ToListAsync(ct);

        var businessIds = businesses.Select(b => b.Id).ToList();

        var contacts = await _context.BusinessContacts
            .Where(c => businessIds.Contains(c.BusinessId) || c.UpdatedAtUtc > lastSync)
            .ToListAsync(ct);

        var audits = await _context.WebsiteAudits
            .Where(a => businessIds.Contains(a.BusinessId) || a.CreatedAtUtc > lastSync)
            .ToListAsync(ct);

        var tasks = await _context.SalesTasks
            .Where(t => businessIds.Contains(t.BusinessId) || t.CreatedAtUtc > lastSync)
            .ToListAsync(ct);

        var notes = await _context.SalesNotes
            .Where(n => businessIds.Contains(n.BusinessId) || n.CreatedAtUtc > lastSync)
            .ToListAsync(ct);

        var messages = await _context.OutreachMessages
            .Where(m => businessIds.Contains(m.BusinessId) || m.UpdatedAtUtc > lastSync)
            .ToListAsync(ct);

        var payload = new Dictionary<string, object>
        {
            ["device_id"] = config.DeviceId,
            ["businesses"] = businesses.Select(b => new
            {
                id = b.Id,
                name = b.Name,
                category = b.Category,
                city = b.City,
                address_line1 = b.AddressLine1,
                postal_code = b.PostalCode,
                latitude = b.Latitude,
                longitude = b.Longitude,
                phone_primary = b.PhonePrimary,
                email_primary = b.EmailPrimary,
                website_url = b.WebsiteUrl,
                website_status = (int)b.WebsiteStatus,
                pipeline_stage_id = b.PipelineStageId,
                lead_status = (int)b.LeadStatus,
                lead_score = b.LeadScore,
                tags = b.Tags,
                is_starred = b.IsStarred,
                source_provider = b.SourceProvider,
                source_external_id = b.SourceReferenceId,
                google_place_id = b.GooglePlaceId,
                google_rating = b.GoogleRating,
                google_review_count = b.GoogleReviewCount,
                is_google_verified = b.IsGoogleVerified,
                business_hours = b.BusinessHours,
                last_enriched_at_utc = b.LastEnrichedAtUtc?.ToString("O"),
                sync_version = b.Version,
                is_deleted = b.IsDeleted
            }),
            ["contacts"] = contacts.Select(c => new
            {
                id = c.Id,
                business_id = c.BusinessId,
                full_name = c.FullName,
                job_title = c.JobTitle,
                phone = c.Phone,
                email = c.Email,
                is_primary = c.IsPrimary,
                sync_version = 1
            }),
            ["website_audits"] = audits.Select(a => new
            {
                id = a.Id,
                business_id = a.BusinessId,
                audited_url = a.TargetUrl,
                overall_score = a.OverallScore ?? 0,
                security_score = a.SecurityScore ?? 0,
                performance_score = a.PerformanceScore ?? 0,
                seo_score = a.SeoScore ?? 0,
                ttfb_ms = a.TtfbMs ?? 0,
                page_size_kb = (double)(a.HtmlSizeBytes ?? 0) / 1024.0,
                ssl_valid = a.SslValid,
                ssl_issuer = a.SslIssuer,
                detected_tech_stack = a.DetectedTechStack,
                sync_version = 1
            }),
            ["sales_tasks"] = tasks.Select(t => new
            {
                id = t.Id,
                business_id = t.BusinessId,
                title = t.Title,
                due_date_utc = t.DueDateUtc?.ToString("O"),
                is_completed = t.IsCompleted,
                completed_at_utc = t.CompletedAtUtc?.ToString("O"),
                sync_version = 1
            }),
            ["sales_notes"] = notes.Select(n => new
            {
                id = n.Id,
                business_id = n.BusinessId,
                content = n.Content,
                author_name = n.AuthorName,
                sync_version = 1
            }),
            ["outreach_messages"] = messages.Select(m => new
            {
                id = m.Id,
                business_id = m.BusinessId,
                contact_id = m.ContactId,
                template_id = m.TemplateId,
                channel = (int)m.Channel,
                recipient_address = m.RecipientAddress,
                subject = m.Subject,
                rendered_body = m.RenderedBody,
                approval_status = (int)m.ApprovalStatus,
                dispatched_at_utc = m.SentAtUtc?.ToString("O"),
                notes = m.Notes,
                sync_version = 1
            })
        };

        var endpoint = string.IsNullOrWhiteSpace(config.ApiToken)
            ? $"{config.ApiBaseUrl.TrimEnd('/')}/dev-sync/push"
            : $"{config.ApiBaseUrl.TrimEnd('/')}/sync/push";

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(config.ApiToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiToken);
        }

        try
        {
            var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return new SyncPushResult
                {
                    Success = false,
                    ErrorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
                };
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            return new SyncPushResult
            {
                Success = root.GetProperty("success").GetBoolean(),
                PushedCount = root.GetProperty("pushed_count").GetInt32(),
                ConflictsCount = root.GetProperty("conflicts_count").GetInt32()
            };
        }
        catch (Exception ex)
        {
            return new SyncPushResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<SyncPullResult> PullRemoteChangesAsync(CancellationToken ct = default)
    {
        var config = await GetConfigAsync(ct);
        var lastSync = config.LastSyncedAtUtc;

        var endpoint = string.IsNullOrWhiteSpace(config.ApiToken)
            ? $"{config.ApiBaseUrl.TrimEnd('/')}/dev-sync/pull"
            : $"{config.ApiBaseUrl.TrimEnd('/')}/sync/pull";

        if (lastSync.HasValue)
        {
            endpoint += $"?since={Uri.EscapeDataString(lastSync.Value.ToString("O"))}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        if (!string.IsNullOrWhiteSpace(config.ApiToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiToken);
        }

        try
        {
            var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return new SyncPullResult
                {
                    Success = false,
                    ErrorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
                };
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var pulledCount = 0;
            var appliedCount = 0;

            // Process remote businesses
            if (root.TryGetProperty("businesses", out var bizArray) && bizArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in bizArray.EnumerateArray())
                {
                    pulledCount++;
                    var id = item.GetProperty("id").GetString();
                    if (string.IsNullOrEmpty(id)) continue;

                    var isDeleted = item.TryGetProperty("is_deleted", out var delProp) && delProp.GetBoolean();
                    var existing = await _context.Businesses.FindAsync([id], ct);

                    if (isDeleted)
                    {
                        if (existing != null)
                        {
                            existing.IsDeleted = true;
                            existing.DeletedAtUtc = DateTime.UtcNow;
                            appliedCount++;
                        }
                    }
                    else
                    {
                        if (existing == null)
                        {
                            var newBiz = new Business
                            {
                                Id = id,
                                Name = item.GetProperty("name").GetString() ?? "Unknown",
                                Category = item.TryGetProperty("category", out var p1) ? p1.GetString() ?? "General" : "General",
                                City = item.TryGetProperty("city", out var p2) ? p2.GetString() ?? "Lahore" : "Lahore",
                                WebsiteUrl = item.TryGetProperty("website_url", out var p3) ? p3.GetString() : null,
                                LeadScore = item.TryGetProperty("lead_score", out var p4) ? p4.GetInt32() : 0,
                                PipelineStageId = item.TryGetProperty("pipeline_stage_id", out var p5) ? p5.GetString() ?? "stage-discovered" : "stage-discovered"
                            };
                            await _context.Businesses.AddAsync(newBiz, ct);
                        }
                        else
                        {
                            existing.Name = item.GetProperty("name").GetString() ?? existing.Name;
                            if (item.TryGetProperty("category", out var p1) && p1.GetString() != null) existing.Category = p1.GetString()!;
                            if (item.TryGetProperty("city", out var p2) && p2.GetString() != null) existing.City = p2.GetString()!;
                            if (item.TryGetProperty("website_url", out var p3)) existing.WebsiteUrl = p3.GetString();
                            if (item.TryGetProperty("lead_score", out var p4)) existing.LeadScore = p4.GetInt32();
                        }
                        appliedCount++;
                    }
                }
            }

            await _context.SaveChangesAsync(ct);

            // Update local sync cursor
            if (root.TryGetProperty("server_timestamp", out var tsProp) && DateTime.TryParse(tsProp.GetString(), out var serverTime))
            {
                config.LastSyncedAtUtc = serverTime.ToUniversalTime();
                await SaveConfigAsync(config, ct);
            }

            return new SyncPullResult
            {
                Success = true,
                PulledCount = pulledCount,
                AppliedCount = appliedCount
            };
        }
        catch (Exception ex)
        {
            return new SyncPullResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<FullSyncResult> SynchronizeAsync(CancellationToken ct = default)
    {
        var pushRes = await PushLocalChangesAsync(ct);
        if (!pushRes.Success)
        {
            return new FullSyncResult
            {
                Success = false,
                ErrorMessage = $"Push failed: {pushRes.ErrorMessage}"
            };
        }

        var pullRes = await PullRemoteChangesAsync(ct);
        if (!pullRes.Success)
        {
            return new FullSyncResult
            {
                Success = false,
                PushedCount = pushRes.PushedCount,
                ConflictsDetected = pushRes.ConflictsCount,
                ErrorMessage = $"Pull failed: {pullRes.ErrorMessage}"
            };
        }

        return new FullSyncResult
        {
            Success = true,
            PushedCount = pushRes.PushedCount,
            PulledCount = pullRes.PulledCount,
            ConflictsDetected = pushRes.ConflictsCount,
            SyncedAtUtc = DateTime.UtcNow
        };
    }

    private async Task<string> GetSettingAsync(string key, string fallback, CancellationToken ct)
    {
        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        return setting?.Value ?? fallback;
    }

    private async Task SaveSettingAsync(string key, string value, CancellationToken ct)
    {
        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting == null)
        {
            setting = new AppSetting { Key = key, Value = value };
            await _context.AppSettings.AddAsync(setting, ct);
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAtUtc = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync(ct);
    }
}
