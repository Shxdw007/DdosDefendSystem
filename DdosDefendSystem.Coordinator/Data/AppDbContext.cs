using Microsoft.EntityFrameworkCore;
using DdosDefendSystem.Shared.Models;

namespace DdosDefendSystem.Coordinator.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<BannedIpInfo> BannedIps { get; set; }
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BannedIpInfo>()
            .HasKey(b => b.IpAddress);
    }
}