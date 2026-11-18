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
}
