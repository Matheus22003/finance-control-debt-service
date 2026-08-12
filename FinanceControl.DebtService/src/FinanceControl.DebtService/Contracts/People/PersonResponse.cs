namespace FinanceControl.DebtService.Contracts.People;

public sealed record PersonResponse(
    Guid Id,
    string Name,
    string? Email,
    bool IsCurrentUser,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
