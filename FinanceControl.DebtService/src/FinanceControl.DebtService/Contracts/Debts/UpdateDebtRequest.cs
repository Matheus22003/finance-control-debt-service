using FinanceControl.DebtService.Domain;

namespace FinanceControl.DebtService.Contracts.Debts;

public sealed record UpdateDebtRequest(
    string Description,
    Guid PaidByPersonId,
    DebtCategory Category,
    DateOnly? DueDate,
    IReadOnlyList<DebtShareRequest> Shares);
