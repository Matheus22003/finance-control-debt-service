namespace FinanceControl.DebtService.Contracts.Users;

public sealed record AccountDeletionEligibilityResponse(
    bool CanDelete,
    int OpenDebtsCount,
    int PendingPaymentsCount,
    int ActiveSettlementPlansCount,
    int OwnedGroupsCount,
    IReadOnlyList<string> Blockers);
