using System.ComponentModel.DataAnnotations;

namespace Synapse.Core.Models.Tenant;

public class Tenant
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Slug { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    [MaxLength(7)]
    public string? PrimaryColor { get; set; }

    [MaxLength(7)]
    public string? SecondaryColor { get; set; }

    [MaxLength(200)]
    public string? CustomDomain { get; set; }

    // ISO 3166-1 alpha-2 — drives invoice routing (PL→KSeF, others→EU PDF)
    [Required, MaxLength(2)]
    public string Country { get; set; } = "PL";

    public decimal VatRatePct { get; set; } = 0.23m;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Business.Business> Businesses { get; set; } = new List<Business.Business>();
}
