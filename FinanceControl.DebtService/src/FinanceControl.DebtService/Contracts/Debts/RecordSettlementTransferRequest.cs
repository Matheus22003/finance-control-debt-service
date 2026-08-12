namespace FinanceControl.DebtService.Contracts.Debts;

public sealed record RecordSettlementTransferRequest(
    Guid? GroupId,
    Guid FromPersonId,
    Guid ToPersonId,
    decimal Amount,
    DateOnly PaymentDate,
    string? Note);
