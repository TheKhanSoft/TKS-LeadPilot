using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.Data;
using TKS.LeadPilot.Data.Repositories;
using Xunit;

namespace TKS.LeadPilot.Data.Tests;

public class BusinessRepositoryFilterTests
{
    private async Task<(AppDbContext Context, string DbPath)> CreateInMemorySqliteAsync()
    {
        var testDbPath = Path.Combine(Path.GetTempPath(), $"tks_filter_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={testDbPath}")
            .Options;

        var context = new AppDbContext(options);
        var initializer = new DatabaseInitializer(context);
        await initializer.InitializeAsync();
        return (context, testDbPath);
    }

    [Fact]
    public async Task GetFilteredAsync_AppliesMultiFieldFilteringAndTagSearch()
    {
        var (context, dbPath) = await CreateInMemorySqliteAsync();
        try
        {
            var repo = new BusinessRepository(context);

            await repo.AddBatchAsync([
                new Business
                {
                    Name = "Karachi Health Hub",
                    Category = "Healthcare",
                    Country = "Pakistan",
                    StateProvince = "Sindh",
                    City = "Karachi",
                    WebsiteUrl = "https://khihealth.pk",
                    LeadScore = 85,
                    PipelineStageId = "stage-discovered",
                    Tags = "VIP, SpeedAudit"
                },
                new Business
                {
                    Name = "Lahore Tech Retail",
                    Category = "Retail",
                    Country = "Pakistan",
                    StateProvince = "Punjab",
                    City = "Lahore",
                    WebsiteUrl = null,
                    LeadScore = 40,
                    PipelineStageId = "stage-discovered",
                    Tags = "NoWebsite"
                },
                new Business
                {
                    Name = "Islamabad Dental Care",
                    Category = "Healthcare",
                    Country = "Pakistan",
                    StateProvince = "Islamabad Capital Territory",
                    City = "Islamabad",
                    WebsiteUrl = "https://isbdental.pk",
                    LeadScore = 90,
                    PipelineStageId = "stage-discovered",
                    Tags = "VIP, Dental"
                }
            ]);

            // 1. Filter by City
            var khiResults = await repo.GetFilteredAsync(new BusinessFilterCriteria { City = "Karachi" });
            khiResults.Should().Contain(b => b.Name == "Karachi Health Hub");

            // 2. Filter by Category
            var healthcareResults = await repo.GetFilteredAsync(new BusinessFilterCriteria { Category = "Healthcare" });
            healthcareResults.Should().Contain(b => b.Name == "Karachi Health Hub");
            healthcareResults.Should().Contain(b => b.Name == "Islamabad Dental Care");

            // 3. Filter by HasWebsite == false
            var noWebsiteResults = await repo.GetFilteredAsync(new BusinessFilterCriteria { HasWebsite = false });
            noWebsiteResults.Should().Contain(b => b.Name == "Lahore Tech Retail");

            // 4. Filter by Tag "VIP"
            var vipResults = await repo.GetFilteredAsync(new BusinessFilterCriteria { Tag = "VIP" });
            vipResults.Should().Contain(b => b.Name == "Karachi Health Hub");
            vipResults.Should().Contain(b => b.Name == "Islamabad Dental Care");

            // 5. Distinct Cities
            var cities = await repo.GetDistinctCitiesAsync();
            cities.Should().Contain(new[] { "Islamabad", "Karachi", "Lahore" });

            // 6. Distinct Tags
            var tags = await repo.GetDistinctTagsAsync();
            tags.Should().Contain(new[] { "VIP", "SpeedAudit", "NoWebsite", "Dental" });

            // 7. Filter by Country and State
            var countryResults = await repo.GetFilteredAsync(new BusinessFilterCriteria { Country = "Pakistan" });
            countryResults.Should().NotBeEmpty();

            var distinctCountries = await repo.GetDistinctCountriesAsync();
            distinctCountries.Should().Contain("Pakistan");

            var distinctStates = await repo.GetDistinctStatesAsync("Pakistan");
            distinctStates.Should().NotBeEmpty();

            // 8. Quick Filter: Hot Leads
            var hotLeads = await repo.GetFilteredAsync(new BusinessFilterCriteria { QuickFilter = QuickFilterType.HotLeads });
            hotLeads.Should().OnlyContain(b => b.LeadScore >= 70);
            hotLeads.Should().Contain(b => b.Name == "Karachi Health Hub");
            hotLeads.Should().NotContain(b => b.Name == "Lahore Tech Retail");
        }
        finally
        {
            context.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task ContactSubresources_AddAndRemoveContactsSuccessfully()
    {
        var (context, dbPath) = await CreateInMemorySqliteAsync();
        try
        {
            var repo = new BusinessRepository(context);

            var biz = new Business
            {
                Name = "Apex Labs",
                Category = "Diagnostic",
                City = "Karachi",
                PipelineStageId = "stage-discovered"
            };
            await repo.AddAsync(biz);

            var contact = new BusinessContact
            {
                BusinessId = biz.Id,
                FullName = "Zainab Khan",
                JobTitle = "Managing Director",
                Email = "zainab@apexlabs.pk",
                Phone = "+92 300 9876543",
                IsPrimary = true
            };
            await repo.AddContactAsync(contact);

            var fetchedBiz = await repo.GetByIdAsync(biz.Id);
            fetchedBiz.Should().NotBeNull();
            fetchedBiz!.Contacts.Should().HaveCount(1);
            fetchedBiz.Contacts[0].FullName.Should().Be("Zainab Khan");
            fetchedBiz.Contacts[0].IsPrimary.Should().BeTrue();

            // Delete contact
            await repo.DeleteContactAsync(contact.Id);
            var reloadedBiz = await repo.GetByIdAsync(biz.Id);
            reloadedBiz!.Contacts.Should().BeEmpty();
        }
        finally
        {
            context.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }
}
