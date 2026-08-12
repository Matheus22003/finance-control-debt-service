namespace FinanceControl.DebtService.Contracts.Debts;

public sealed record PaymentRequest(
    decimal Amount,
    DateOnly PaymentDate,
    string? Note);
