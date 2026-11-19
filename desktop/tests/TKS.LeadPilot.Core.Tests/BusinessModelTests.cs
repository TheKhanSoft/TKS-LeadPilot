using FluentAssertions;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Models;
using Xunit;

namespace TKS.LeadPilot.Core.Tests;

public class BusinessModelTests
{
    [Fact]
    public void NewBusiness_HasValidUuidAndDefaultTimestamps()
    {
        // Act
        var business = new Business
        {
            Name = "Test Healthcare Clinic",
            City = "Lahore",
            Category = "Healthcare"
        };

        // Assert
        business.Id.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(business.Id, out _).Should().BeTrue("Entity ID must be a valid RFC 4122 UUID");
        business.IsDeleted.Should().BeFalse();
        business.Version.Should().Be(1);
        business.Country.Should().Be("Pakistan");
        business.WebsiteStatus.Should().Be(WebsiteStatus.NoWebsite);
        business.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SoftDelete_SetsIsDeletedAndTimestamp()
    {
        // Arrange
        var business = new Business
        {
            Name = "Soft Delete Test Co",
            City = "Karachi",
            Category = "Retail"
        };

        // Act
        business.IsDeleted = true;
        business.DeletedAtUtc = DateTime.UtcNow;
        business.Version++;

        // Assert
        business.IsDeleted.Should().BeTrue();
        business.DeletedAtUtc.Should().NotBeNull();
        business.Version.Should().Be(2);
    }

    [Fact]
    public void IsSelected_FiresPropertyChangedEvent()
    {
        var business = new Business { Name = "Test Co" };
        var changedProps = new List<string>();
        business.PropertyChanged += (s, e) => changedProps.Add(e.PropertyName!);

        business.IsSelected = true;

        business.IsSelected.Should().BeTrue();
        changedProps.Should().Contain(nameof(Business.IsSelected));
    }

    [Fact]
    public void IsStarred_FiresPropertyChangedAndUpdatesStarDisplay()
    {
        var business = new Business { Name = "Test Co" };
        var changedProps = new List<string>();
        business.PropertyChanged += (s, e) => changedProps.Add(e.PropertyName!);

        business.StarDisplay.Should().Be("☆");
        business.IsStarred = true;

        business.StarDisplay.Should().Be("★");
        changedProps.Should().Contain(nameof(Business.IsStarred));
        changedProps.Should().Contain(nameof(Business.StarDisplay));
    }

    [Fact]
    public void FullAddress_CombinesAllComponents_WithoutDuplicatingTokens()
    {
        var business = new Business
        {
            Name = "TheKhanSoft Solutions",
            AddressLine1 = "Plot 12-B, Main Boulevard",
            Locality = "Gulberg III",
            City = "Lahore",
            StateProvince = "Punjab",
            PostalCode = "54000",
            Country = "Pakistan"
        };

        var full = business.FullAddress;
        full.Should().Be("Plot 12-B, Main Boulevard, Gulberg III, Lahore, Punjab, 54000, Pakistan");
    }

    [Fact]
    public void FullAddress_FiresPropertyChanged_WhenAddressFieldsChange()
    {
        var business = new Business { Name = "Test Co", City = "Islamabad" };
        var changedProps = new List<string>();
        business.PropertyChanged += (s, e) => changedProps.Add(e.PropertyName!);

        business.AddressLine1 = "Sector F-7/2, Street 15";

        changedProps.Should().Contain(nameof(Business.AddressLine1));
        changedProps.Should().Contain(nameof(Business.FullAddress));
        business.FullAddress.Should().Contain("Sector F-7/2, Street 15");
        business.FullAddress.Should().Contain("Islamabad");
    }

    [Fact]
    public void FullAddress_HandlesPartialOrMissingAddress_Gracefully()
    {
        var businessOnlyCity = new Business { City = "Karachi", Country = "Pakistan" };
        businessOnlyCity.FullAddress.Should().Be("Karachi, Pakistan");

        var businessWithNoStreet = new Business
        {
            Locality = "DHA Phase 5",
            City = "Lahore",
            Country = "Pakistan"
        };
        businessWithNoStreet.FullAddress.Should().Be("DHA Phase 5, Lahore, Pakistan");
    }
}
