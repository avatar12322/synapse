using System.ComponentModel.DataAnnotations;
using NetTopologySuite.Geometries;

namespace Synapse.Core.Models.Business;

public class Business
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? City { get; set; }

    // PostGIS geography point (SRID 4326 — WGS84). X=longitude, Y=latitude.
    public Point Location { get; set; } = null!;

    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Owner user account (Role = Business)
    public int OwnerId { get; set; }
    public User.User Owner { get; set; } = null!;

    // Stripe Connect account for commission payouts
    [MaxLength(100)]
    public string? StripeAccountId { get; set; }

    public bool StripeOnboardingComplete { get; set; } = false;

    // Default discount offered to mission participants
    public int DefaultDiscountPercent { get; set; } = 15;

    // Phase 2: HMAC-SHA256 key for POS webhook signature verification
    [MaxLength(100)]
    public string? PosWebhookSecret { get; set; }

    // Phase 2: HMAC-SHA256 key for NFC NDEF tag signature verification
    [MaxLength(100)]
    public string? NfcSecret { get; set; }

    // Phase 3: H3 res-6 cell (~36 km²) — stable shard key derived from Location
    [MaxLength(20)]
    public string? H3Index { get; set; }

    // Phase 4: null = global Synapse platform, non-null = white-label tenant
    public int? TenantId { get; set; }
    public Tenant.Tenant? Tenant { get; set; }

    public ICollection<Mission.Mission> Missions { get; set; } = new List<Mission.Mission>();
    public ICollection<Invoice.KsefInvoice> KsefInvoices { get; set; } = new List<Invoice.KsefInvoice>();
}
