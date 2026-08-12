namespace FinanceControl.DebtService.Domain;

public sealed class SettlementTransfer
{
    private SettlementTransfer()
    {
    }

    internal SettlementTransfer(
        Guid fromIdentityId,
        Guid? fromUserId,
        Guid fromPersonId,
        string fromPersonName,
        Guid toIdentityId,
        Guid? toUserId,
        Guid toPersonId,
        string toPersonName,
        decimal amount,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        FromIdentityId = fromIdentityId;
        FromUserId = fromUserId;
        FromPersonId = fromPersonId;
        FromPersonName = fromPersonName;
        ToIdentityId = toIdentityId;
        ToUserId = toUserId;
        ToPersonId = toPersonId;
        ToPersonName = toPersonName;
        Amount = amount;
        Status = SettlementTransferStatus.AwaitingPayment;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid SettlementPlanId { get; private set; }
    public Guid FromIdentityId { get; private set; }
    public Guid? FromUserId { get; private set; }
    public Guid FromPersonId { get; private set; }
    public string FromPersonName { get; private set; } = string.Empty;
    public Guid ToIdentityId { get; private set; }
    public Guid? ToUserId { get; private set; }
    public Guid ToPersonId { get; private set; }
    public string ToPersonName { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public DateOnly? PaymentDate { get; private set; }
    public string? Note { get; private set; }
    public Guid? RecordedByUserId { get; private set; }
    public SettlementTransferStatus Status { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public DateTimeOffset? RejectedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Record(Guid userId, DateOnly paymentDate, string? note, DateTimeOffset now)
    {
        if (Status != SettlementTransferStatus.AwaitingPayment || FromUserId != userId)
        {
            throw new InvalidOperationException("Only the suggested payer can record this transfer.");
        }

        if (ToUserId is null)
        {
            throw new InvalidOperationException(
                "The recipient must be a registered user to confirm a simplified transfer.");
        }

        PaymentDate = paymentDate;
        Note = note;
        RecordedByUserId = userId;
        Status = SettlementTransferStatus.Pending;
        UpdatedAt = now;
    }

    public void Confirm(Guid userId, DateTimeOffset now)
    {
        if (Status != SettlementTransferStatus.Pending || ToUserId != userId)
        {
            throw new InvalidOperationException(
                "This transfer is not awaiting confirmation from the authenticated user.");
        }

        Status = SettlementTransferStatus.Confirmed;
        ConfirmedAt = now;
        UpdatedAt = now;
    }

    public void Reject(Guid userId, DateTimeOffset now)
    {
        if (Status != SettlementTransferStatus.Pending || ToUserId != userId)
        {
            throw new InvalidOperationException(
                "This transfer is not awaiting confirmation from the authenticated user.");
        }

        Status = SettlementTransferStatus.Rejected;
        RejectedAt = now;
        UpdatedAt = now;
    }

    internal void AnonymizeDeletedUser(Guid userId, DateTimeOffset now)
    {
        if (FromUserId == userId)
        {
            FromUserId = null;
            FromIdentityId = FromPersonId;
            FromPersonName = "Usuário removido";
        }

        if (ToUserId == userId)
        {
            ToUserId = null;
            ToIdentityId = ToPersonId;
            ToPersonName = "Usuário removido";
        }

        if (RecordedByUserId == userId)
        {
            RecordedByUserId = null;
        }

        UpdatedAt = now;
    }
}
