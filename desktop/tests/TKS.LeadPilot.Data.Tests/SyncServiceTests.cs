using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.Data;
using TKS.LeadPilot.Data.Services;
using Xunit;

namespace TKS.LeadPilot.Data.Tests;

public class SyncServiceTests
{
    private class MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handlerFunc) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handlerFunc(request));
        }
    }

    private class InMemorySecretVault : ISecretVault
    {
        private readonly System.Collections.Generic.Dictionary<string, string> _store = [];
        public void SaveSecret(string key, string secret) => _store[key] = secret;
        public string? GetSecret(string key) => _store.TryGetValue(key, out var val) ? val : null;
        public bool HasSecret(string key) => _store.ContainsKey(key);
        public void DeleteSecret(string key) => _store.Remove(key);
    }

    private static async Task<(AppDbContext Context, string DbPath)> CreateTestContextAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"tks_sync_test_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var initializer = new DatabaseInitializer(context);
        await initializer.InitializeAsync();

        return (context, dbPath);
    }

    [Fact]
    public async Task GetConfigAsync_ReturnsDefaultConfig_WhenNotConfigured()
    {
        var (context, dbPath) = await CreateTestContextAsync();
        try
        {
            var service = new SyncService(context);
            var config = await service.GetConfigAsync();

            config.Should().NotBeNull();
            config.ApiBaseUrl.Should().Be("http://127.0.0.1:8000/api/v1");
            config.DeviceId.Should().NotBeNullOrWhiteSpace();
            config.DeviceName.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            await context.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task SaveConfigAsync_PersistsSettingsAndVaultToken()
    {
        var (context, dbPath) = await CreateTestContextAsync();
        var vault = new InMemorySecretVault();
        try
        {
            var service = new SyncService(context, vault);
            var config = new SyncConfig
            {
                ApiBaseUrl = "https://leadpilot.thekhansoft.com/api/v1",
                ApiToken = "sanctum_secret_token_123",
                DeviceId = "dev-unique-uuid-999",
                DeviceName = "Field Laptop",
                LastSyncedAtUtc = DateTime.UtcNow
            };

            await service.SaveConfigAsync(config);

            var loaded = await service.GetConfigAsync();
            loaded.ApiBaseUrl.Should().Be("https://leadpilot.thekhansoft.com/api/v1");
            loaded.DeviceId.Should().Be("dev-unique-uuid-999");
            loaded.DeviceName.Should().Be("Field Laptop");
            loaded.ApiToken.Should().Be("sanctum_secret_token_123");
        }
        finally
        {
            await context.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task GetSyncStatusAsync_WhenServerIsOnline_ParsesStatusAndPendingCount()
    {
        var (context, dbPath) = await CreateTestContextAsync();
        var mockHttp = new MockHttpMessageHandler(req =>
        {
            req.RequestUri!.AbsolutePath.Should().EndWith("/health");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"healthy\",\"version\":\"1.0.0\"}", Encoding.UTF8, "application/json")
            };
        });

        try
        {
            var httpClient = new HttpClient(mockHttp);
            var service = new SyncService(context, null, httpClient);

            var status = await service.GetSyncStatusAsync();

            status.IsOnline.Should().BeTrue();
            status.ServerMessage.Should().Be("healthy");
            status.ServerVersion.Should().Be("1.0.0");
        }
        finally
        {
            await context.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task PushLocalChangesAsync_WhenServerReturnsSuccess_ReportsPushedAndConflictCount()
    {
        var (context, dbPath) = await CreateTestContextAsync();
        var mockHttp = new MockHttpMessageHandler(req =>
        {
            req.Method.Should().Be(HttpMethod.Post);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"success\":true,\"pushed_count\":3,\"conflicts_count\":0}", Encoding.UTF8, "application/json")
            };
        });

        try
        {
            var httpClient = new HttpClient(mockHttp);
            var service = new SyncService(context, null, httpClient);

            var result = await service.PushLocalChangesAsync();

            result.Success.Should().BeTrue();
            result.PushedCount.Should().Be(3);
            result.ConflictsCount.Should().Be(0);
        }
        finally
        {
            await context.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task PullRemoteChangesAsync_AppliesRemoteBusinessesToLocalDatabase()
    {
        var (context, dbPath) = await CreateTestContextAsync();
        var remoteBizId = Guid.NewGuid().ToString();

        var mockHttp = new MockHttpMessageHandler(req =>
        {
            req.Method.Should().Be(HttpMethod.Get);
            var json = $$"""
            {
                "server_timestamp": "2026-11-17T12:00:00Z",
                "businesses": [
                    {
                        "id": "{{remoteBizId}}",
                        "name": "Frontier Dental Care",
                        "category": "Healthcare",
                        "city": "Peshawar",
                        "website_url": "https://frontier.pk",
                        "lead_score": 75,
                        "pipeline_stage_id": "stage-discovered",
                        "is_deleted": false
                    }
                ]
            }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        try
        {
            var httpClient = new HttpClient(mockHttp);
            var service = new SyncService(context, null, httpClient);

            var result = await service.PullRemoteChangesAsync();

            result.Success.Should().BeTrue();
            result.PulledCount.Should().Be(1);
            result.AppliedCount.Should().Be(1);

            var saved = await context.Businesses.FindAsync(remoteBizId);
            saved.Should().NotBeNull();
            saved!.Name.Should().Be("Frontier Dental Care");
            saved.City.Should().Be("Peshawar");
            saved.LeadScore.Should().Be(75);
        }
        finally
        {
            await context.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }
}
