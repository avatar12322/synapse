using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Synapse.Core.Models.User;
using Synapse.Core.Models.Social;
using Synapse.Core.Models.Notification;
using Synapse.Core.Models.Business;
using Synapse.Core.Models.Mission;
using Synapse.Core.Models.Invoice;
using Synapse.Core.Models.Tenant;

namespace Synapse.Infrastructure.Data;

public class SynapseDbContext : DbContext
{
    public SynapseDbContext(DbContextOptions<SynapseDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<UserProfile> UserProfiles { get; set; }
    public DbSet<UserReputation> UserReputations { get; set; }
    public DbSet<ReputationTransaction> ReputationTransactions { get; set; }
    public DbSet<Friendship> Friendships { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Business> Businesses { get; set; }
    public DbSet<Mission> Missions { get; set; }
    public DbSet<KsefInvoice> KsefInvoices { get; set; }
    public DbSet<Tenant> Tenants { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Enable pgvector extension (migration will call CREATE EXTENSION IF NOT EXISTS vector)
        modelBuilder.HasPostgresExtension("vector");
        // PostGIS is enabled via UseNetTopologySuite() in Program.cs
        modelBuilder.HasPostgresExtension("postgis");


        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.Username).IsUnique();
        });

        modelBuilder.Entity<UserProfile>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.UserId).IsUnique();

            e.HasOne(p => p.User)
             .WithMany()
             .HasForeignKey(p => p.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            // pgvector column with IVFFlat index (created after first batch of embeddings)
            e.Property(p => p.Embedding)
             .HasColumnType("vector(768)");
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

        modelBuilder.Entity<Tenant>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.Slug).IsUnique();
            e.HasIndex(t => t.CustomDomain);
            e.Property(t => t.VatRatePct).HasColumnType("decimal(5,4)");
        });

        modelBuilder.Entity<Business>(e =>
        {
            e.HasKey(b => b.Id);
            e.HasIndex(b => b.City);
            e.HasIndex(b => b.Category);
            e.HasIndex(b => b.H3Index);
            e.HasIndex(b => b.TenantId);

            // Geography type gives metre-based distance calculations
            e.Property(b => b.Location)
             .HasColumnType("geography(Point, 4326)");

            // GIST spatial index for ST_DWithin queries
            e.HasIndex(b => b.Location)
             .HasMethod("GIST");

            e.HasOne(b => b.Owner)
             .WithMany()
             .HasForeignKey(b => b.OwnerId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(b => b.Tenant)
             .WithMany(t => t.Businesses)
             .HasForeignKey(b => b.TenantId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<KsefInvoice>(e =>
        {
            e.HasKey(i => i.Id);
            e.HasIndex(i => new { i.BusinessId, i.Status });
            e.HasIndex(i => i.KsefReferenceNumber);
            e.Property(i => i.VatRatePct).HasColumnType("decimal(5,4)");

            e.HasOne(i => i.Business)
             .WithMany(b => b.KsefInvoices)
             .HasForeignKey(i => i.BusinessId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserReputation>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.UserId).IsUnique();

            e.HasOne(r => r.User)
             .WithMany()
             .HasForeignKey(r => r.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReputationTransaction>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => new { t.UserReputationId, t.CreatedAt });

            e.HasOne(t => t.UserReputation)
             .WithMany(r => r.Transactions)
             .HasForeignKey(t => t.UserReputationId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Mission>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => m.Status);
            e.HasIndex(m => new { m.UserAId, m.Status });
            e.HasIndex(m => new { m.UserBId, m.Status });
            e.HasIndex(m => m.VerificationCode);
            e.HasIndex(m => m.H3Index);

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
