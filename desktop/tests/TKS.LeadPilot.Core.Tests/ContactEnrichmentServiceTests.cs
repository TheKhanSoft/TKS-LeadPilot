using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.Core.Services;
using Xunit;

namespace TKS.LeadPilot.Core.Tests;

public class ContactEnrichmentServiceTests
{
    private class FakeSsrfValidator : ISsrfValidator
    {
        public Task<SsrfValidationResult> ValidateUrlAsync(string? url, CancellationToken ct = default)
            => Task.FromResult(new SsrfValidationResult { IsSafe = true });
    }

    private class FakeBusinessRepository : IBusinessRepository
    {
        public List<Business> Storage { get; } = [];

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
            Storage.AddRange(businesses);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Business business, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateBatchAsync(IEnumerable<Business> businesses, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(string id, CancellationToken ct = default)
        {
            Storage.RemoveAll(b => b.Id == id);
            return Task.CompletedTask;
        }

        public Task DeleteBatchAsync(IEnumerable<string> ids, CancellationToken ct = default)
        {
            var idSet = ids.ToHashSet();
            Storage.RemoveAll(b => idSet.Contains(b.Id));
            return Task.CompletedTask;
        }

        public Task DeleteAllAsync(CancellationToken ct = default)
        {
            Storage.Clear();
            return Task.CompletedTask;
        }

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

    private class FakeHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }

    [Theory]
    [InlineData("0300 1234567", "Pakistan", "923001234567")]
    [InlineData("+92 333 9988776", "Pakistan", "923339988776")]
    [InlineData("0345-1122334", "Pakistan", "923451122334")]
    [InlineData("(518) 555-0123", "United States", "15185550123")]
    public void CleanPhoneNumber_NormalizesCorrectly(string input, string country, string expected)
    {
        var service = new ContactEnrichmentService(new HttpClient(), new FakeSsrfValidator(), new FakeBusinessRepository());
        var result = service.CleanPhoneNumber(input, country);
        result.Should().Be(expected);
    }

    [Fact]
    public void FormatWhatsAppUrl_ReturnsDirectChatLink()
    {
        var service = new ContactEnrichmentService(new HttpClient(), new FakeSsrfValidator(), new FakeBusinessRepository());
        var url = service.FormatWhatsAppUrl("0300 8554433", "Pakistan");
        url.Should().Be("https://wa.me/923008554433");
    }

    [Fact]
    public async Task EnrichBusinessContacts_NormalizesPhoneToWhatsApp()
    {
        var service = new ContactEnrichmentService(new HttpClient(), new FakeSsrfValidator(), new FakeBusinessRepository());
        var biz = new Business
        {
            Id = "biz-1",
            Name = "Apex Dental Clinic",
            City = "Peshawar",
            Country = "Pakistan",
            PhonePrimary = "0300 1234567"
        };

        var result = await service.EnrichBusinessContactsAsync(biz);

        result.IsSuccess.Should().BeTrue();
        biz.WhatsAppNumber.Should().Be("923001234567");
        biz.WhatsAppDirectUrl.Should().Be("https://wa.me/923001234567");
        biz.HasWhatsApp.Should().BeTrue();
    }

    [Fact]
    public async Task EnrichBusinessContacts_ExtractsEmailFromHtml()
    {
        var htmlContent = """
            <html>
                <body>
                    <p>Contact our support team at info@apexdental.pk or call us.</p>
                    <a href="mailto:appointments@apexdental.pk">Book Online</a>
                    <img src="logo@2x.png" />
                </body>
            </html>
            """;

        var fakeResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(htmlContent)
        };

        var client = new HttpClient(new FakeHttpMessageHandler(fakeResponse));
        var service = new ContactEnrichmentService(client, new FakeSsrfValidator(), new FakeBusinessRepository());

        var biz = new Business
        {
            Id = "biz-2",
            Name = "Apex Dental",
            City = "Islamabad",
            Country = "Pakistan",
            WebsiteUrl = "https://apexdental.pk"
        };

        var result = await service.EnrichBusinessContactsAsync(biz);

        result.IsSuccess.Should().BeTrue();
        biz.EmailPrimary.Should().NotBeNullOrWhiteSpace();
        biz.EmailPrimary.Should().Contain("@apexdental.pk");
        result.DiscoveredEmails.Should().NotContain("logo@2x.png");
    }

    [Fact]
    public void Business_WhatsAppDirectUrl_ComputesFromPhonePrimary_WhenWhatsAppNumberMissing()
    {
        var biz = new Business
        {
            PhonePrimary = "0312 9988776",
            Country = "Pakistan"
        };

        biz.HasWhatsApp.Should().BeTrue();
        biz.WhatsAppDirectUrl.Should().Be("https://wa.me/923129988776");
    }
}
