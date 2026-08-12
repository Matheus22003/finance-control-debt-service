namespace FinanceControl.DebtService.Contracts.People;

public sealed record PersonRequest(
    string Name,
    string? Email,
    bool IsCurrentUser);
