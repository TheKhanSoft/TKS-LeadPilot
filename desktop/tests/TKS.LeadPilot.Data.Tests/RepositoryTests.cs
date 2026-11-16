using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.Data;
using TKS.LeadPilot.Data.Repositories;
using Xunit;

namespace TKS.LeadPilot.Data.Tests;

public class RepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly BusinessRepository _repository;

    public RepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();

        // Seed a default pipeline stage for foreign key constraints
        _context.PipelineStages.Add(new PipelineStage
        {
            Id = "stage-qual",
            Name = "Qualified",
            Code = "QUALIFIED",
            DisplayOrder = 1
        });
        _context.SaveChanges();

        _repository = new BusinessRepository(_context);
    }

    [Fact]
    public async Task AddAsync_PersistsBusinessToSQLite()
    {
        // Arrange
        var business = new Business
        {
            Name = "Horizon Engineering Labs",
            City = "Faisalabad",
            Category = "Engineering",
            PipelineStageId = "stage-qual",
            LeadScore = 85
        };

        // Act
        var created = await _repository.AddAsync(business);

        // Assert
        created.Should().NotBeNull();
        var retrieved = await _repository.GetByIdAsync(created.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Horizon Engineering Labs");
        retrieved.City.Should().Be("Faisalabad");
        retrieved.LeadScore.Should().Be(85);
    }

    [Fact]
    public async Task GetAllAsync_FiltersBySearchQuery()
    {
        // Arrange
        await _repository.AddAsync(new Business
        {
            Name = "Green Valley Cafe",
            City = "Islamabad",
            Category = "Food",
            PipelineStageId = "stage-qual"
        });

        await _repository.AddAsync(new Business
        {
            Name = "Blue Sky Logistics",
            City = "Karachi",
            Category = "Logistics",
            PipelineStageId = "stage-qual"
        });

        // Act
        var results = await _repository.GetAllAsync(searchQuery: "Valley");

        // Assert
        results.Should().HaveCount(1);
        results.First().Name.Should().Be("Green Valley Cafe");
    }

    [Fact]
    public async Task DeleteAsync_PerformsSoftDelete()
    {
        // Arrange
        var business = await _repository.AddAsync(new Business
        {
            Name = "To Delete Co",
            City = "Lahore",
            Category = "Services",
            PipelineStageId = "stage-qual"
        });

        // Act
        await _repository.DeleteAsync(business.Id);

        // Assert
        var activeBusinesses = await _repository.GetAllAsync();
        activeBusinesses.Should().NotContain(b => b.Id == business.Id);

        var totalActive = await _repository.GetTotalCountAsync();
        totalActive.Should().Be(0);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
