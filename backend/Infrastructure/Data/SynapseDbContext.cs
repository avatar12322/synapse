using Microsoft.EntityFrameworkCore;
using Synapse.Core.Models.User;
using Synapse.Core.Models.Social;
using Synapse.Core.Models.Notification;
using Synapse.Core.Models.Business;
using Synapse.Core.Models.Mission;

namespace Synapse.Infrastructure.Data;

public class SynapseDbContext : DbContext
{
    public SynapseDbContext(DbContextOptions<SynapseDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Friendship> Friendships { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Business> Businesses { get; set; }
    public DbSet<Mission> Missions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.Username).IsUnique();
        });

        modelBuilder.Entity<Friendship>(e =>
        {
            e.HasKey(f => f.Id);
            e.HasIndex(f => new { f.UserId, f.FriendId }).IsUnique();

            e.HasOne(f => f.User)
             .WithMany(u => u.InitiatedFriendships)
             .HasForeignKey(f => f.UserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(f => f.Friend)
             .WithMany(u => u.ReceivedFriendships)
             .HasForeignKey(f => f.FriendId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.HasKey(n => n.Id);
            e.HasIndex(n => new { n.UserId, n.IsRead });

            e.HasOne(n => n.User)
             .WithMany()
             .HasForeignKey(n => n.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Business>(e =>
        {
            e.HasKey(b => b.Id);
            e.HasIndex(b => b.City);
            e.HasIndex(b => b.Category);

            e.HasOne(b => b.Owner)
             .WithMany()
             .HasForeignKey(b => b.OwnerId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Mission>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => m.Status);
            e.HasIndex(m => new { m.UserAId, m.Status });
            e.HasIndex(m => new { m.UserBId, m.Status });

            e.HasOne(m => m.Business)
             .WithMany(b => b.Missions)
             .HasForeignKey(m => m.BusinessId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(m => m.UserA)
             .WithMany()
             .HasForeignKey(m => m.UserAId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(m => m.UserB)
             .WithMany()
             .HasForeignKey(m => m.UserBId)
             .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
