namespace FinanceControl.DebtService.Domain;

public enum SettlementTransferStatus
{
    AwaitingPayment,
    Pending,
    Confirmed,
    Rejected
}
