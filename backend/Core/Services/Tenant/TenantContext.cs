using TenantEntity = Synapse.Core.Models.Tenant.Tenant;

namespace Synapse.Core.Services.Tenant;

public interface ITenantContext
{
    TenantEntity? Current { get; }
    bool HasTenant { get; }
}

public class TenantContext : ITenantContext
{
    public TenantEntity? Current { get; set; }
    public bool HasTenant => Current is not null;
}
