namespace FinanceControl.DebtService.Domain;

public sealed class DebtHistory
{
    private DebtHistory()
    {
    }

    internal DebtHistory(DebtHistoryType type, string description, DateTimeOffset occurredAt)
    {
        Id = Guid.NewGuid();
        Type = type;
        Description = description;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }
    public Guid DebtId { get; private set; }
    public DebtHistoryType Type { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
}
