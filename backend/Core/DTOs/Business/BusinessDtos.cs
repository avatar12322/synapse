using System.ComponentModel.DataAnnotations;

namespace Synapse.Core.DTOs.Business;

public record CreateBusinessRequest(
    [Required, MaxLength(100)] string Name,
    [Required, MaxLength(200)] string Address,
    string? City,
    double Latitude,
    double Longitude,
    [Required, MaxLength(50)] string Category,
    string? Description,
    int DefaultDiscountPercent = 15
);

public record UpdateBusinessRequest(
    string? Name,
    string? Address,
    string? City,
    double? Latitude,
    double? Longitude,
    string? Category,
    string? Description,
    bool? IsActive,
    int? DefaultDiscountPercent
);

public record BusinessDto(
    int Id,
    string Name,
    string Address,
    string? City,
    double Latitude,
    double Longitude,
    string Category,
    string? Description,
    bool IsActive,
    int DefaultDiscountPercent,
    bool StripeOnboardingComplete,
    double? DistanceMetres
);

public record NearbyBusinessesRequest(
    [Required] double Latitude,
    [Required] double Longitude,
    int RadiusMetres = 2000,
    string? Category = null
);
