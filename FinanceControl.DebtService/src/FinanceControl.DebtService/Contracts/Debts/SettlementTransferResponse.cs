using FinanceControl.DebtService.Domain;

namespace FinanceControl.DebtService.Contracts.Debts;

public sealed record SettlementTransferResponse(
    Guid Id,
    Guid SettlementPlanId,
    Guid? GroupId,
    Guid FromIdentityId,
    PersonReferenceResponse FromPerson,
    Guid ToIdentityId,
    PersonReferenceResponse ToPerson,
    decimal Amount,
    DateOnly? PaymentDate,
    string? Note,
    SettlementTransferStatus Status,
    bool CanRecord,
    bool CanConfirm,
    bool CanReject,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? RejectedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
