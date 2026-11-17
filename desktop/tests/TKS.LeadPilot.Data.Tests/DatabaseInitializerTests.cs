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
                businesses.Should().HaveCount(8, "8 realistic commercial Pakistani businesses must be seeded across key cities");
                businesses.Should().Contain(b => b.Name.Contains("Apex Dental"));
                businesses.Should().Contain(b => b.Name.Contains("Careway Pharmacy"));
                businesses.Should().Contain(b => b.Name.Contains("Al-Rehman Textile"));

                var audits = await context.WebsiteAudits.Include(a => a.Findings).ToListAsync();
                audits.Should().HaveCount(2, "Website audits must be seeded for businesses with active websites");
                audits.First().Findings.Should().NotBeEmpty();

                var scores = await context.LeadScores.Include(s => s.Components).ToListAsync();
                scores.Should().HaveCount(3, "Explainable lead scores must be seeded");

                var tasks = await context.SalesTasks.ToListAsync();
                tasks.Should().HaveCount(4, "Sales tasks must be seeded");

                var notes = await context.SalesNotes.ToListAsync();
                notes.Should().HaveCount(3, "Sales notes must be seeded");

                var templates = await context.OutreachTemplates.ToListAsync();
                templates.Should().HaveCount(4, "Outreach templates must be seeded");

                var messages = await context.OutreachMessages.ToListAsync();
                messages.Should().HaveCount(2, "Pre-generated outreach drafts must be seeded in pending approval status");
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
