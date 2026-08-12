namespace FinanceControl.DebtService.Domain;

public sealed class Payment
{
    private Payment()
    {
    }

    internal Payment(
        Guid debtShareId,
        decimal amount,
        DateOnly paymentDate,
        string? note,
        Guid recordedByUserId,
        Guid? confirmationRequiredFromUserId,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        DebtShareId = debtShareId;
        Amount = amount;
        PaymentDate = paymentDate;
        Note = note;
        RecordedByUserId = recordedByUserId;
        ConfirmationRequiredFromUserId = confirmationRequiredFromUserId;
        Status = confirmationRequiredFromUserId is null
            ? PaymentStatus.Confirmed
            : PaymentStatus.Pending;
        ConfirmedAt = Status == PaymentStatus.Confirmed ? now : null;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid DebtId { get; private set; }
    public Guid DebtShareId { get; private set; }
    public DebtShare DebtShare { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public DateOnly PaymentDate { get; private set; }
    public string? Note { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public Guid? ConfirmationRequiredFromUserId { get; private set; }
    public PaymentStatus Status { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public DateTimeOffset? RejectedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    internal void Update(
        Guid userId,
        decimal amount,
        DateOnly paymentDate,
        string? note,
        DateTimeOffset now)
    {
        EnsureRecorder(userId);
        if (Status != PaymentStatus.Pending)
        {
            throw new InvalidOperationException("Only pending payments can be updated.");
        }

        Amount = amount;
        PaymentDate = paymentDate;
        Note = note;
        UpdatedAt = now;
    }

    internal void Confirm(Guid userId, DateTimeOffset now)
    {
        EnsurePendingConfirmation(userId);
        Status = PaymentStatus.Confirmed;
        ConfirmedAt = now;
        UpdatedAt = now;
    }

    internal void Reject(Guid userId, DateTimeOffset now)
    {
        EnsurePendingConfirmation(userId);
        Status = PaymentStatus.Rejected;
        RejectedAt = now;
        UpdatedAt = now;
    }

    internal void EnsureCanDelete(Guid userId)
    {
        EnsureRecorder(userId);
    }

    internal void AnonymizeDeletedUser(Guid userId, DateTimeOffset now)
    {
        if (RecordedByUserId == userId)
        {
            RecordedByUserId = Guid.Empty;
        }

        if (ConfirmationRequiredFromUserId == userId)
        {
            ConfirmationRequiredFromUserId = null;
        }

        UpdatedAt = now;
    }

    private void EnsureRecorder(Guid userId)
    {
        if (RecordedByUserId != userId)
        {
            throw new InvalidOperationException("Only the user who recorded the payment can change it.");
        }
    }

    private void EnsurePendingConfirmation(Guid userId)
    {
        if (Status != PaymentStatus.Pending || ConfirmationRequiredFromUserId != userId)
        {
            throw new InvalidOperationException("This payment is not awaiting confirmation from this user.");
        }
    }
}
