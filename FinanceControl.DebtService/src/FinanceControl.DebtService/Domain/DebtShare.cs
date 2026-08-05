namespace FinanceControl.DebtService.Domain;

public sealed class DebtShare
{
    private DebtShare()
    {
    }

    internal DebtShare(Guid personId, decimal amount, bool isPayer)
    {
        Id = Guid.NewGuid();
        PersonId = personId;
        Amount = amount;
        PaidAmount = isPayer ? amount : 0m;
    }

    public Guid Id { get; private set; }
    public Guid DebtId { get; private set; }
    public Guid PersonId { get; private set; }
    public Person Person { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal RemainingAmount => Amount - PaidAmount;

    internal void AddPayment(decimal amount)
    {
        PaidAmount += amount;
    }

    internal void RemovePayment(decimal amount)
    {
        PaidAmount -= amount;
    }

    internal void Update(decimal amount, bool wasPayer, bool isPayer)
    {
        Amount = amount;
        if (isPayer)
        {
            PaidAmount = amount;
        }
        else if (wasPayer)
        {
            PaidAmount = 0m;
        }
    }
}
