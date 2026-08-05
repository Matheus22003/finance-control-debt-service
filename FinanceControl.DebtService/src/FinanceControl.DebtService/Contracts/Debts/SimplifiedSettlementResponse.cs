namespace FinanceControl.DebtService.Contracts.Debts;

public sealed record SimplifiedSettlementResponse(
    decimal TotalOpenAmount,
    int OriginalTransferCount,
    int SimplifiedTransferCount,
    IReadOnlyList<SimplifiedTransferResponse> Transfers);

public sealed record SimplifiedTransferResponse(
    Guid FromIdentityId,
    PersonReferenceResponse FromPerson,
    Guid ToIdentityId,
    PersonReferenceResponse ToPerson,
    decimal Amount);
