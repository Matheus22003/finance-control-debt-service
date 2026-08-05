using FinanceControl.DebtService.Domain;

namespace FinanceControl.DebtService.Contracts.Debts;

public sealed record DebtResponse(
    Guid Id,
    string Description,
    decimal TotalAmount,
    PersonReferenceResponse PaidBy,
    Guid? GroupId,
    DebtCategory Category,
    DebtStatus Status,
    DateOnly? DueDate,
    bool CreatedByCurrentUser,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<DebtShareResponse> Shares);

public sealed record PersonReferenceResponse(
    Guid Id,
    string Name,
    bool IsCurrentUser);

public sealed record DebtShareResponse(
    Guid Id,
    PersonReferenceResponse Person,
    decimal Amount,
    decimal PaidAmount,
    decimal RemainingAmount,
    bool IsPayer);
