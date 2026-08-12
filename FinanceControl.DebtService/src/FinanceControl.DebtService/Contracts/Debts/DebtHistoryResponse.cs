using FinanceControl.DebtService.Domain;

namespace FinanceControl.DebtService.Contracts.Debts;

public sealed record DebtHistoryResponse(
    Guid Id,
    DebtHistoryType Type,
    string Description,
    DateTimeOffset OccurredAt);
