namespace Synapse.Core.DTOs.Tenant;

public record TenantDto(
    int Id,
    string Slug,
    string Name,
    string? LogoUrl,
    string? PrimaryColor,
    string? SecondaryColor,
    string? CustomDomain,
    string Country,
    decimal VatRatePct,
    bool IsActive,
    DateTime CreatedAt);

public record BrandingDto(
    string Name,
    string? LogoUrl,
    string PrimaryColor,
    string SecondaryColor);

public record CreateTenantRequest(
    string Slug,
    string Name,
    string Country,
    decimal VatRatePct,
    string? LogoUrl,
    string? PrimaryColor,
    string? SecondaryColor,
    string? CustomDomain);

public record UpdateTenantRequest(
    string? Name,
    string? LogoUrl,
    string? PrimaryColor,
    string? SecondaryColor,
    string? CustomDomain,
    decimal? VatRatePct,
    bool? IsActive);
