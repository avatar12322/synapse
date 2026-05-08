using System.ComponentModel.DataAnnotations;

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

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Owner user account (Role = Business)
    public int OwnerId { get; set; }
    public User.User Owner { get; set; } = null!;

    public ICollection<Mission.Mission> Missions { get; set; } = new List<Mission.Mission>();
}
