using System;
using System.Collections.Generic;
using FluentAssertions;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.Core.Services;
using Xunit;

namespace TKS.LeadPilot.Core.Tests;

public class DeduplicationServiceTests
{
    private readonly DeduplicationService _sut = new();

    [Theory]
    [InlineData("https://www.carewaypharmacy.com/about?ref=123", "carewaypharmacy.com")]
    [InlineData("http://CarewayPharmacy.com:8080/store/", "carewaypharmacy.com")]
    [InlineData("carewaypharmacy.com#section", "carewaypharmacy.com")]
    [InlineData("  https://SUBDOMAIN.example.org/path/  ", "subdomain.example.org")]
    public void NormalizeDomain_StripsProtocolsPathsQueriesAndPorts(string input, string expected)
    {
        var result = _sut.NormalizeDomain(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("+92 300 1234567", "923001234567")]
    [InlineData("0300-1234567", "923001234567")]
    [InlineData("(021) 3456-7890", "922134567890")]
    [InlineData("00923001234567", "923001234567")]
    public void NormalizePhone_NormalizesPakistanPhoneFormats(string input, string expected)
    {
        var result = _sut.NormalizePhone(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Careway Pharmacy & Diagnostic Pvt Ltd.", "careway pharmacy diagnostic")]
    [InlineData("Apex Dental Services Co., LLC", "apex dental")]
    [InlineData("The Grand Hotel & Suites", "grand hotel suites")]
    public void NormalizeName_RemovesPunctuationAndNoiseWords(string input, string expected)
    {
        var result = _sut.NormalizeName(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void CheckDuplicate_IdentifiesExactWebsiteMatch_With100Confidence()
    {
        var existing = new List<Business>
        {
            new()
            {
                Id = "b1",
                Name = "Careway Pharmacy",
                City = "Karachi",
                WebsiteUrl = "https://www.careway.pk"
            }
        };

        var incoming = new Business
        {
            Name = "Careway Health Store",
            City = "Karachi",
            WebsiteUrl = "http://careway.pk/home"
        };

        var match = _sut.CheckDuplicate(incoming, existing);

        match.IsDuplicate.Should().BeTrue();
        match.MatchType.Should().Be(DuplicateMatchType.ExactWebsite);
        match.ConfidenceScore.Should().Be(100);
        match.ExistingBusiness!.Id.Should().Be("b1");
    }

    [Fact]
    public void CheckDuplicate_IdentifiesExactPhoneMatch_With95Confidence()
    {
        var existing = new List<Business>
        {
            new()
            {
                Id = "b1",
                Name = "Apex Dental Clinic",
                City = "Lahore",
                PhonePrimary = "+92 42 35781234"
            }
        };

        var incoming = new Business
        {
            Name = "Dr. Apex Dentistry",
            City = "Lahore",
            PhonePrimary = "042-3578-1234"
        };

        var match = _sut.CheckDuplicate(incoming, existing);

        match.IsDuplicate.Should().BeTrue();
        match.MatchType.Should().Be(DuplicateMatchType.ExactPhone);
        match.ConfidenceScore.Should().Be(95);
        match.ExistingBusiness!.Id.Should().Be("b1");
    }

    [Fact]
    public void CheckDuplicate_IdentifiesExactNameAndCity_With90Confidence()
    {
        var existing = new List<Business>
        {
            new()
            {
                Id = "b1",
                Name = "Sunrise Grand Hotel Pvt Ltd",
                City = "Islamabad"
            }
        };

        var incoming = new Business
        {
            Name = "Sunrise Grand Hotel",
            City = "Islamabad"
        };

        var match = _sut.CheckDuplicate(incoming, existing);

        match.IsDuplicate.Should().BeTrue();
        match.MatchType.Should().Be(DuplicateMatchType.ExactNameAndCity);
        match.ConfidenceScore.Should().Be(90);
    }

    [Fact]
    public void MergeBusinesses_FillsMissingFields_AndMergesTagsAndContacts()
    {
        var target = new Business
        {
            Id = "b1",
            Name = "Careway Pharmacy",
            City = "Karachi",
            PhonePrimary = null,
            EmailPrimary = null,
            Tags = "Retail"
        };

        var source = new Business
        {
            Name = "Careway Pharmacy",
            PhonePrimary = "+92 21 34567890",
            EmailPrimary = "info@careway.pk",
            WebsiteUrl = "https://careway.pk",
            Tags = "Retail, Healthcare, SpeedAudit",
            Contacts =
            [
                new BusinessContact
                {
                    FullName = "Dr. Tariq",
                    JobTitle = "Head Pharmacist",
                    Email = "tariq@careway.pk"
                }
            ]
        };

        _sut.MergeBusinesses(target, source);

        target.PhonePrimary.Should().Be("+92 21 34567890");
        target.EmailPrimary.Should().Be("info@careway.pk");
        target.WebsiteUrl.Should().Be("https://careway.pk");
        target.GetTagList().Should().Contain(["Retail", "Healthcare", "SpeedAudit"]);
        target.Contacts.Should().HaveCount(1);
        target.Contacts[0].FullName.Should().Be("Dr. Tariq");
        target.Contacts[0].BusinessId.Should().Be("b1");
    }
}
