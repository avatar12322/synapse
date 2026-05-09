using Microsoft.EntityFrameworkCore;
using Stripe;
using Synapse.Infrastructure.Data;

namespace Synapse.Core.Services.Stripe;

public interface IStripeService
{
    Task<string> CreateConnectOnboardingLinkAsync(int businessId, string returnUrl, string refreshUrl, CancellationToken ct = default);
    Task<bool> CompleteOnboardingAsync(int businessId, string accountId, CancellationToken ct = default);
    Task<bool> ChargeCommissionAsync(int missionId, CancellationToken ct = default);
}

public class StripeService(SynapseDbContext db) : IStripeService
{
    public async Task<string> CreateConnectOnboardingLinkAsync(
        int businessId, string returnUrl, string refreshUrl, CancellationToken ct)
    {
        var business = await db.Businesses.FindAsync([businessId], ct)
            ?? throw new KeyNotFoundException($"Business {businessId} not found.");

        var accountService = new AccountService();
        string accountId;

        if (string.IsNullOrEmpty(business.StripeAccountId))
        {
            var account = await accountService.CreateAsync(new AccountCreateOptions
            {
                Type = "standard",
                Country = "PL",
                Email = business.Owner.Email,
            }, cancellationToken: ct);
            accountId = account.Id;
            business.StripeAccountId = accountId;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            accountId = business.StripeAccountId;
        }

        var linkService = new AccountLinkService();
        var link = await linkService.CreateAsync(new AccountLinkCreateOptions
        {
            Account = accountId,
            RefreshUrl = refreshUrl,
            ReturnUrl = returnUrl,
            Type = "account_onboarding",
        }, cancellationToken: ct);

        return link.Url;
    }

    public async Task<bool> CompleteOnboardingAsync(int businessId, string accountId, CancellationToken ct)
    {
        var business = await db.Businesses.FindAsync([businessId], ct);
        if (business is null) return false;

        var accountService = new AccountService();
        var account = await accountService.GetAsync(accountId, cancellationToken: ct);

        business.StripeAccountId = accountId;
        business.StripeOnboardingComplete = account.ChargesEnabled;
        await db.SaveChangesAsync(ct);
        return business.StripeOnboardingComplete;
    }

    public async Task<bool> ChargeCommissionAsync(int missionId, CancellationToken ct)
    {
        var mission = await db.Missions
            .Include(m => m.Business)
            .FirstOrDefaultAsync(m => m.Id == missionId, ct);

        if (mission is null || string.IsNullOrEmpty(mission.Business.StripeAccountId))
            return false;

        if (mission.CommissionPaid || mission.CommissionAmountCents <= 0)
            return false;

        // Platform charges business via transfer reversal pattern
        // Commission is e.g. 15% of assumed 20 PLN average spend = 3 PLN = 300 gr
        var commissionCents = mission.CommissionAmountCents > 0
            ? mission.CommissionAmountCents
            : 300; // 3 PLN default

        var transferService = new TransferService();
        await transferService.CreateAsync(new TransferCreateOptions
        {
            Amount = commissionCents,
            Currency = "pln",
            Destination = mission.Business.StripeAccountId,
            Description = $"Synapse mission #{missionId} commission refund",
        }, cancellationToken: ct);

        mission.CommissionPaid = true;
        await db.SaveChangesAsync(ct);
        return true;
    }
}
