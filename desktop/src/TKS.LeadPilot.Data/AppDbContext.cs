using Microsoft.EntityFrameworkCore;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<BusinessContact> BusinessContacts => Set<BusinessContact>();
    public DbSet<PipelineStage> PipelineStages => Set<PipelineStage>();
    public DbSet<WebsiteAudit> WebsiteAudits => Set<WebsiteAudit>();
    public DbSet<WebsiteAuditFinding> WebsiteAuditFindings => Set<WebsiteAuditFinding>();
    public DbSet<LeadScore> LeadScores => Set<LeadScore>();
    public DbSet<LeadScoreComponent> LeadScoreComponents => Set<LeadScoreComponent>();
    public DbSet<TksService> TksServices => Set<TksService>();
    public DbSet<SalesTask> SalesTasks => Set<SalesTask>();
    public DbSet<SalesNote> SalesNotes => Set<SalesNote>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Business
        modelBuilder.Entity<Business>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.Property(e => e.City).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Country).IsRequired().HasMaxLength(100);
            
            entity.HasIndex(e => new { e.City, e.Category });
            entity.HasIndex(e => e.LeadScore);
            entity.HasIndex(e => e.PipelineStageId);
            entity.HasIndex(e => e.GooglePlaceId);

            entity.HasOne(e => e.PipelineStage)
                .WithMany()
                .HasForeignKey(e => e.PipelineStageId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // BusinessContact
        modelBuilder.Entity<BusinessContact>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(255);

            entity.HasOne(e => e.Business)
                .WithMany(b => b.Contacts)
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PipelineStage
        modelBuilder.Entity<PipelineStage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        // WebsiteAudit
        modelBuilder.Entity<WebsiteAudit>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TargetUrl).IsRequired().HasMaxLength(500);

            entity.HasOne(e => e.Business)
                .WithMany(b => b.Audits)
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // WebsiteAuditFinding
        modelBuilder.Entity<WebsiteAuditFinding>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(255);

            entity.HasOne(e => e.Audit)
                .WithMany(a => a.Findings)
                .HasForeignKey(e => e.AuditId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // LeadScore
        modelBuilder.Entity<LeadScore>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.BusinessId);
        });

        // LeadScoreComponent
        modelBuilder.Entity<LeadScoreComponent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne<LeadScore>()
                .WithMany(s => s.Components)
                .HasForeignKey(e => e.LeadScoreId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // SalesTask
        modelBuilder.Entity<SalesTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(255);

            entity.HasOne(e => e.Business)
                .WithMany(b => b.Tasks)
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // SalesNote
        modelBuilder.Entity<SalesNote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();

            entity.HasOne(e => e.Business)
                .WithMany(b => b.Notes)
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AppSetting
        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(100);
        });
    }
}
