using FeedHorn.Models;
using Microsoft.EntityFrameworkCore;

namespace FeedHorn.Data;

public class FeedHornContext : DbContext
{
    public FeedHornContext(DbContextOptions<FeedHornContext> options) : base(options)
    {
    }

    public DbSet<MonitoredUrl> MonitoredUrls { get; set; }
    public DbSet<UrlCheck> UrlChecks { get; set; }
    public DbSet<SpeedTest> SpeedTests { get; set; }
    public DbSet<SslCertificate> SslCertificates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MonitoredUrl>()
            .HasMany(u => u.Checks)
            .WithOne(c => c.MonitoredUrl)
            .HasForeignKey(c => c.MonitoredUrlId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MonitoredUrl>()
            .HasIndex(u => u.Url)
            .IsUnique();
    }
}
