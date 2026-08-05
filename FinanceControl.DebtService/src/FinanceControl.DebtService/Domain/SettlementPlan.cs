namespace FinanceControl.DebtService.Domain;

public sealed class SettlementPlan
{
    private readonly List<SettlementTransfer> _transfers = [];
    private readonly List<SettlementAllocation> _allocations = [];

    private SettlementPlan()
    {
    }

    public SettlementPlan(
        Guid createdByUserId,
        Guid? groupId,
        IReadOnlyCollection<SettlementTransferDraft> transfers,
        IReadOnlyCollection<SettlementAllocationDraft> allocations,
        DateTimeOffset now)
    {
        if (transfers.Count == 0 || allocations.Count == 0)
        {
            throw new InvalidOperationException("A settlement plan requires transfers and allocations.");
        }

        Id = Guid.NewGuid();
        CreatedByUserId = createdByUserId;
        GroupId = groupId;
        Status = SettlementPlanStatus.Active;
        CreatedAt = now;
        UpdatedAt = now;
        _transfers.AddRange(transfers.Select(transfer => new SettlementTransfer(
            transfer.FromIdentityId,
            transfer.FromUserId,
            transfer.FromPersonId,
            transfer.FromPersonName,
            transfer.ToIdentityId,
            transfer.ToUserId,
            transfer.ToPersonId,
            transfer.ToPersonName,
            transfer.Amount,
            now)));
        _allocations.AddRange(allocations.Select(allocation => new SettlementAllocation(
            allocation.DebtId,
            allocation.DebtShareId,
            allocation.Amount)));
    }

    public Guid Id { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? GroupId { get; private set; }
    public SettlementPlanStatus Status { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<SettlementTransfer> Transfers => _transfers;
    public IReadOnlyCollection<SettlementAllocation> Allocations => _allocations;

    public SettlementTransfer FindTransfer(Guid transferId) =>
        _transfers.SingleOrDefault(transfer => transfer.Id == transferId)
        ?? throw new InvalidOperationException("The settlement transfer was not found in this plan.");

    public void EnsureActive()
    {
        if (Status != SettlementPlanStatus.Active)
        {
            throw new InvalidOperationException("The settlement plan is no longer active.");
        }
    }

    public void Complete(DateTimeOffset now)
    {
        EnsureActive();
        if (_transfers.Any(transfer => transfer.Status != SettlementTransferStatus.Confirmed))
        {
            throw new InvalidOperationException("Every settlement transfer must be confirmed first.");
        }

        Status = SettlementPlanStatus.Completed;
        CompletedAt = now;
        UpdatedAt = now;
    }

    public void Cancel(DateTimeOffset now)
    {
        if (Status != SettlementPlanStatus.Active)
        {
            return;
        }

        Status = SettlementPlanStatus.Cancelled;
        CancelledAt = now;
        UpdatedAt = now;
    }

    public void AnonymizeDeletedUser(Guid userId, DateTimeOffset now)
    {
        if (CreatedByUserId == userId)
        {
            CreatedByUserId = Guid.Empty;
        }

        foreach (var transfer in _transfers)
        {
            transfer.AnonymizeDeletedUser(userId, now);
        }

        UpdatedAt = now;
    }
}

public sealed record SettlementTransferDraft(
    Guid FromIdentityId,
    Guid? FromUserId,
    Guid FromPersonId,
    string FromPersonName,
    Guid ToIdentityId,
    Guid? ToUserId,
    Guid ToPersonId,
    string ToPersonName,
    decimal Amount);

public sealed record SettlementAllocationDraft(
    Guid DebtId,
    Guid DebtShareId,
    decimal Amount);
