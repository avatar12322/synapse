using Synapse.Core.Models.Invoice;

namespace Synapse.Core.DTOs.Invoice;

public record InvoiceRequestDto(int BusinessId, DateOnly PeriodStart, DateOnly PeriodEnd);

public record InvoiceStatusDto(
    Guid Id,
    int BusinessId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int TotalAmountCents,
    int MissionCount,
    string Status,
    string? KsefReferenceNumber,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? UpoReceivedAt,
    string? ErrorMessage,
    // Phase 4: jurisdiction routing
    string InvoiceType,
    decimal VatRatePct
);
