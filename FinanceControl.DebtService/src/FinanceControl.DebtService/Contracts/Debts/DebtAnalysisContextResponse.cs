namespace FinanceControl.DebtService.Contracts.Debts;

public sealed record DebtAnalysisContextResponse(
    DateTimeOffset GeneratedAt,
    decimal TotalOwed,
    decimal TotalToReceive,
    int OpenDebtsCount,
    int PaidDebtsCount,
    int OverdueDebtsCount,
    int DueSoonDebtsCount,
    IReadOnlyList<DebtAnalysisCategoryResponse> Categories,
    IReadOnlyList<DebtAnalysisGroupResponse> Groups,
    IReadOnlyList<DebtAnalysisDriverResponse> TopDrivers);

public sealed record DebtAnalysisCategoryResponse(
    string Category,
    decimal TotalOwed,
    decimal TotalToReceive,
    int OpenDebtsCount);

public sealed record DebtAnalysisGroupResponse(
    Guid? GroupId,
    string? GroupName,
    decimal TotalOwed,
    decimal TotalToReceive,
    int OpenDebtsCount);

public sealed record DebtAnalysisDriverResponse(
    string Category,
    Guid? GroupId,
    string? GroupName,
    decimal TotalOwed,
    decimal TotalToReceive,
    DateOnly? DueDate,
    bool IsOverdue);
