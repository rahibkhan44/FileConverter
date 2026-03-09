using System.Text.Json;
using FileConverter.Domain.Enums;
using FileConverter.Domain.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FileConverter.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public DbSet<ConversionJob> Jobs => Set<ConversionJob>();
    public DbSet<BatchConversionJob> BatchJobs => Set<BatchConversionJob>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Identity tables

        modelBuilder.Entity<ConversionJob>(e =>
        {
            e.HasKey(j => j.Id);
            e.Property(j => j.OriginalFileName).HasMaxLength(500);
            e.Property(j => j.InputFilePath).HasMaxLength(1000);
            e.Property(j => j.OutputFilePath).HasMaxLength(1000);
            e.Property(j => j.ErrorMessage).HasMaxLength(2000);
            e.Property(j => j.SourceFormat).HasConversion<string>().HasMaxLength(20);
            e.Property(j => j.TargetFormat).HasConversion<string>().HasMaxLength(20);
            e.Property(j => j.Status).HasConversion<string>().HasMaxLength(20);

            e.Property(j => j.Options).HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new()
            ).HasColumnType("TEXT");

            e.HasIndex(j => j.Status);
            e.HasIndex(j => j.CreatedAt);
            e.HasIndex(j => j.BatchJobId);
        });

        modelBuilder.Entity<BatchConversionJob>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.TargetFormat).HasConversion<string>().HasMaxLength(20);

            e.Ignore(b => b.Status);
            e.Ignore(b => b.OverallProgress);

            e.HasMany(b => b.Jobs)
                .WithOne()
                .HasForeignKey(j => j.BatchJobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApiKey>(e =>
        {
            e.HasKey(k => k.Id);
            e.Property(k => k.UserId).HasMaxLength(450);
            e.Property(k => k.KeyHash).HasMaxLength(128);
            e.Property(k => k.KeyPrefix).HasMaxLength(10);
            e.Property(k => k.Name).HasMaxLength(100);
            e.HasIndex(k => k.KeyHash).IsUnique();
            e.HasIndex(k => k.UserId);
        });
    }
}
