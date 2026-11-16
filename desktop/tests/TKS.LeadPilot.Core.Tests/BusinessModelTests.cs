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
}
