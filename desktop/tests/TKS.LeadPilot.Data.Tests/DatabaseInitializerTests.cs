using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TKS.LeadPilot.Data;
using Xunit;

namespace TKS.LeadPilot.Data.Tests;

public class DatabaseInitializerTests
{
    [Fact]
    public async Task InitializeAsync_CreatesTablesAndSeedsInitialData()
    {
        // Arrange
        var testDbPath = Path.Combine(Path.GetTempPath(), $"tks_test_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={testDbPath}")
            .Options;

        try
        {
            using (var context = new AppDbContext(options))
            {
                var initializer = new DatabaseInitializer(context);

                // Act
                await initializer.InitializeAsync();

                // Assert
                var stages = await context.PipelineStages.ToListAsync();
                stages.Should().HaveCount(14, "14 standard pipeline stages must be seeded");

                var services = await context.TksServices.ToListAsync();
                services.Should().HaveCount(4, "4 core TKS service offerings must be seeded");

                var businesses = await context.Businesses.Include(b => b.Contacts).ToListAsync();
                businesses.Should().HaveCount(3, "3 initial sample businesses must be seeded for immediate metrics");
                businesses.Should().Contain(b => b.Name.Contains("Apex Dental"));
                businesses.Should().Contain(b => b.Name.Contains("Careway Pharmacy"));
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(testDbPath))
            {
                File.Delete(testDbPath);
            }
        }
    }
}
