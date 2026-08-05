namespace FinanceControl.DebtService.Domain;

public sealed class SettlementAllocation
{
    private SettlementAllocation()
    {
    }

    internal SettlementAllocation(Guid debtId, Guid debtShareId, decimal amount)
    {
        Id = Guid.NewGuid();
        DebtId = debtId;
        DebtShareId = debtShareId;
        Amount = amount;
    }

    public Guid Id { get; private set; }
    public Guid SettlementPlanId { get; private set; }
    public Guid DebtId { get; private set; }
    public Guid DebtShareId { get; private set; }
    public decimal Amount { get; private set; }
}
