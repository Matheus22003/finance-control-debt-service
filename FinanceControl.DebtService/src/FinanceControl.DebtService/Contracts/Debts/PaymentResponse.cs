using FinanceControl.DebtService.Domain;

namespace FinanceControl.DebtService.Contracts.Debts;

public sealed record PaymentResponse(
    Guid Id,
    Guid DebtId,
    Guid DebtShareId,
    PersonReferenceResponse FromPerson,
    PersonReferenceResponse ToPerson,
    decimal Amount,
    DateOnly PaymentDate,
    string? Note,
    Guid RecordedByUserId,
    Guid? ConfirmationRequiredFromUserId,
    PaymentStatus Status,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? RejectedAt,
    bool CanConfirm,
    bool CanReject,
    bool CanEdit,
    bool CanDelete,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
