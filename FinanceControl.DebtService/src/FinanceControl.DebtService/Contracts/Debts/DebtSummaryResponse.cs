namespace FinanceControl.DebtService.Contracts.Debts;

public sealed record DebtSummaryResponse(
    decimal TotalOwed,
    decimal TotalToReceive,
    int OpenDebtsCount);
