using FinanceControl.DebtService.Domain;

namespace FinanceControl.DebtService.Contracts.Debts;

public sealed record CreateDebtRequest(
    string Description,
    decimal TotalAmount,
    Guid PaidByPersonId,
    Guid? GroupId,
    DebtCategory Category,
    DateOnly? DueDate,
    IReadOnlyList<DebtShareRequest> Shares);

public sealed record DebtShareRequest(
    Guid PersonId,
    decimal Amount);
