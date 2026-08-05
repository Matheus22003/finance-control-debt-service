namespace FinanceControl.DebtService.Domain;

public enum DebtHistoryType
{
    Created,
    Updated,
    PaymentAdded,
    PaymentUpdated,
    PaymentDeleted,
    PaymentPending,
    PaymentConfirmed,
    PaymentRejected,
    SplitUpdated,
    Paid,
    Reopened
}
