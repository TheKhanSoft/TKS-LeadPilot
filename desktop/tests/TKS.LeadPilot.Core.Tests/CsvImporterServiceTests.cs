using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.Core.Services;
using Xunit;

namespace TKS.LeadPilot.Core.Tests;

public class CsvImporterServiceTests
{
    private readonly FakeBusinessRepository _repository = new();
    private readonly DeduplicationService _dedup = new();
    private readonly CsvImporterService _sut;

    public CsvImporterServiceTests()
    {
        _sut = new CsvImporterService(_repository, _dedup);
    }

    [Fact]
    public void AutoDetectMapping_MapsStandardAndSynonymousHeaders()
    {
        var headers = new List<string> { "Business Name", "Industry", "Web", "Mobile", "Contact Email", "Town", "Labels" };

        var mapping = _sut.AutoDetectMapping(headers);

        mapping.NameColumn.Should().Be("Business Name");
        mapping.CategoryColumn.Should().Be("Industry");
        mapping.WebsiteColumn.Should().Be("Web");
        mapping.PhoneColumn.Should().Be("Mobile");
        mapping.EmailColumn.Should().Be("Contact Email");
        mapping.CityColumn.Should().Be("Town");
        mapping.TagsColumn.Should().Be("Labels");
    }

    [Fact]
    public async Task PreviewCsvAsync_ParsesValidRowsAndIdentifiesDuplicates()
    {
        var csvContent = @"Business Name,Category,City,Website,Phone
Apex Dental,Healthcare,Lahore,https://apexdental.pk,+924235781234
Blue Ocean Marine,Logistics,Karachi,https://blueocean.pk,021-35678901
Invalid Row,,Islamabad,,
";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        _repository.Seed([
            new Business
            {
                Id = "existing-1",
                Name = "Apex Dental Care",
                City = "Lahore",
                WebsiteUrl = "https://www.apexdental.pk"
            }
        ]);

        var preview = await _sut.PreviewCsvAsync(stream);

        preview.TotalRows.Should().Be(3);
        preview.ValidRows.Should().Be(3);
        preview.DuplicateRows.Should().Be(1, "Apex Dental has matching normalized domain");
        preview.PreviewRows[0].DuplicateMatch.Should().NotBeNull();
        preview.PreviewRows[0].DuplicateMatch!.IsDuplicate.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteImportAsync_WithSkipStrategy_InsertsOnlyNonDuplicates()
    {
        var csvContent = @"Name,Category,City,Website
Careway Pharmacy,Pharmacy,Karachi,https://careway.pk
Novelty Bookshop,Retail,Islamabad,https://noveltybooks.pk
";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        _repository.Seed([
            new Business
            {
                Id = "existing-1",
                Name = "Careway Pharmacy",
                City = "Karachi",
                WebsiteUrl = "https://careway.pk"
            }
        ]);

        var result = await _sut.ExecuteImportAsync(stream, strategy: DeduplicationStrategy.Skip);

        result.TotalProcessed.Should().Be(2);
        result.CreatedCount.Should().Be(1);
        result.SkippedCount.Should().Be(1);

        _repository.AddedBatch.Should().HaveCount(1);
        _repository.AddedBatch[0].Name.Should().Be("Novelty Bookshop");
    }

    [Fact]
    public async Task ExportToCsvAsync_OutputsValidCsvWithUtf8Bom()
    {
        var businesses = new List<Business>
        {
            new()
            {
                Id = "b1",
                Name = "The \"Elite\" Diagnostic Center, Karachi",
                Category = "Healthcare",
                City = "Karachi",
                WebsiteUrl = "https://elitediagnostic.pk",
                Tags = "VIP, Diagnostics"
            }
        };

        var bytes = await _sut.ExportToCsvAsync(businesses);

        bytes.Length.Should().BeGreaterThan(0);
        // UTF-8 BOM check
        bytes[0].Should().Be(0xEF);
        bytes[1].Should().Be(0xBB);
        bytes[2].Should().Be(0xBF);

        var content = Encoding.UTF8.GetString(bytes[3..]);
        content.Should().Contain("\"The \"\"Elite\"\" Diagnostic Center, Karachi\"");
        content.Should().Contain("elitediagnostic.pk");
    }

    private class FakeBusinessRepository : IBusinessRepository
    {
        public List<Business> Storage { get; } = [];
        public List<Business> AddedBatch { get; } = [];
        public List<Business> UpdatedBatch { get; } = [];

        public void Seed(IEnumerable<Business> businesses) => Storage.AddRange(businesses);

        public Task<List<Business>> GetAllAsync(string? searchQuery = null, string? city = null, string? category = null, string? stageId = null, CancellationToken ct = default)
            => Task.FromResult(Storage.ToList());

        public Task<List<Business>> GetFilteredAsync(BusinessFilterCriteria criteria, CancellationToken ct = default)
            => Task.FromResult(Storage.ToList());

        public Task<int> GetFilteredCountAsync(BusinessFilterCriteria criteria, CancellationToken ct = default)
            => Task.FromResult(Storage.Count);

        public Task<Business?> GetByIdAsync(string id, CancellationToken ct = default)
            => Task.FromResult(Storage.FirstOrDefault(b => b.Id == id));

        public Task<Business> AddAsync(Business business, CancellationToken ct = default)
        {
            Storage.Add(business);
            return Task.FromResult(business);
        }

        public Task AddBatchAsync(IEnumerable<Business> businesses, CancellationToken ct = default)
        {
            AddedBatch.AddRange(businesses);
            Storage.AddRange(businesses);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Business business, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateBatchAsync(IEnumerable<Business> businesses, CancellationToken ct = default)
        {
            UpdatedBatch.AddRange(businesses);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetTotalCountAsync(CancellationToken ct = default) => Task.FromResult(Storage.Count);
        public Task<int> GetWithWebsiteCountAsync(CancellationToken ct = default) => Task.FromResult(Storage.Count(b => !string.IsNullOrEmpty(b.WebsiteUrl)));
        public Task<int> GetHighOpportunityCountAsync(int threshold = 70, CancellationToken ct = default) => Task.FromResult(0);
        public Task<List<string>> GetDistinctCountriesAsync(CancellationToken ct = default) => Task.FromResult(new List<string>());
        public Task<List<string>> GetDistinctStatesAsync(string? country = null, CancellationToken ct = default) => Task.FromResult(new List<string>());
        public Task<List<string>> GetDistinctCitiesAsync(CancellationToken ct = default) => Task.FromResult(Storage.Select(b => b.City).Distinct().ToList());
        public Task<List<string>> GetDistinctCategoriesAsync(CancellationToken ct = default) => Task.FromResult(Storage.Select(b => b.Category).Distinct().ToList());
        public Task<List<string>> GetDistinctTagsAsync(CancellationToken ct = default) => Task.FromResult(new List<string>());
        public Task<List<Business>> FindPotentialDuplicatesAsync(string? domain, string? phone, string? name, string? city, CancellationToken ct = default) => Task.FromResult(new List<Business>());
        public Task AddContactAsync(BusinessContact contact, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateContactAsync(BusinessContact contact, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteContactAsync(string contactId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
