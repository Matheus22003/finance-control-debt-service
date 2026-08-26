namespace FinanceControl.DebtService.Contracts.Debts;

public sealed record DebtReportResponse(
    DateOnly FromDate,
    DateOnly ToDate,
    decimal TotalVolume,
    decimal TotalOwed,
    decimal TotalToReceive,
    int OpenDebtsCount,
    int PaidDebtsCount,
    IReadOnlyList<DebtReportMonthResponse> Months,
    IReadOnlyList<DebtReportCategoryResponse> Categories,
    IReadOnlyList<DebtReportItemResponse> TopDebts);

public sealed record DebtReportMonthResponse(
    string ReferenceMonth,
    decimal TotalVolume,
    decimal TotalOwed,
    decimal TotalToReceive,
    int DebtCount);

public sealed record DebtReportCategoryResponse(
    string Category,
    decimal TotalVolume,
    decimal TotalOwed,
    decimal TotalToReceive,
    int DebtCount);

public sealed record DebtReportItemResponse(
    Guid Id,
    string Description,
    string Category,
    decimal TotalAmount,
    decimal TotalOwed,
    decimal TotalToReceive,
    string Status,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt);
