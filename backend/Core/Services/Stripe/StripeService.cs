using Microsoft.EntityFrameworkCore;
using Stripe;
using Synapse.Infrastructure.Data;

namespace Synapse.Core.Services.Stripe;

public interface IStripeService
{
    Task<string> CreateConnectOnboardingLinkAsync(int businessId, string returnUrl, string refreshUrl, CancellationToken ct = default);
    Task<bool> CompleteOnboardingAsync(int businessId, string accountId, CancellationToken ct = default);
    Task<bool> ChargeCommissionAsync(int missionId, CancellationToken ct = default);
    Task<bool> HandleWebhookAsync(string json, string stripeSignature, string webhookSecret, CancellationToken ct = default);
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

    public async Task<bool> HandleWebhookAsync(string json, string stripeSignature, string webhookSecret, CancellationToken ct)
    {
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, webhookSecret);
        }
        catch (StripeException)
        {
            return false;
        }

        switch (stripeEvent.Type)
        {
            case EventTypes.AccountUpdated:
            {
                if (stripeEvent.Data.Object is not Account account) break;
                var business = await db.Businesses
                    .FirstOrDefaultAsync(b => b.StripeAccountId == account.Id, ct);
                if (business is null) break;
                business.StripeOnboardingComplete = account.ChargesEnabled;
                await db.SaveChangesAsync(ct);
                break;
            }

            case EventTypes.AccountApplicationDeauthorized:
            {
                if (stripeEvent.Data.Object is not Application app) break;
                // account id is on the event's account field for deauth events
                var accountId = stripeEvent.Account;
                if (string.IsNullOrEmpty(accountId)) break;
                var business = await db.Businesses
                    .FirstOrDefaultAsync(b => b.StripeAccountId == accountId, ct);
                if (business is null) break;
                business.StripeOnboardingComplete = false;
                await db.SaveChangesAsync(ct);
                break;
            }

            case "transfer.paid":
            {
                if (stripeEvent.Data.Object is not Transfer transfer) break;
                // Match transfer description to mission id: "Synapse mission #<id> commission refund"
                var desc = transfer.Description ?? string.Empty;
                var prefix = "Synapse mission #";
                var suffix = " commission refund";
                if (!desc.StartsWith(prefix) || !desc.EndsWith(suffix)) break;
                var idStr = desc[prefix.Length..^suffix.Length];
                if (!int.TryParse(idStr, out var missionId)) break;
                var mission = await db.Missions.FindAsync([missionId], ct);
                if (mission is null) break;
                mission.CommissionPaid = true;
                await db.SaveChangesAsync(ct);
                break;
            }
        }

        return true;
    }
}
