namespace FinanceControl.DebtService.Domain;

public sealed class Debt
{
    private readonly List<DebtShare> _shares = [];
    private readonly List<Payment> _payments = [];
    private readonly List<DebtHistory> _history = [];

    private Debt()
    {
    }

    public Debt(
        Guid createdByUserId,
        string description,
        decimal totalAmount,
        Guid paidByPersonId,
        Guid? groupId,
        DebtCategory category,
        DateOnly? dueDate,
        IReadOnlyCollection<(Guid PersonId, decimal Amount)> shares,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        CreatedByUserId = createdByUserId;
        Description = description;
        TotalAmount = totalAmount;
        PaidByPersonId = paidByPersonId;
        GroupId = groupId;
        Category = category;
        DueDate = dueDate;
        Status = DebtStatus.Open;
        CreatedAt = now;
        UpdatedAt = now;

        foreach (var share in shares)
        {
            _shares.Add(new DebtShare(
                share.PersonId,
                share.Amount,
                share.PersonId == paidByPersonId));
        }

        _history.Add(new DebtHistory(DebtHistoryType.Created, "Debt created.", now));
        RecalculateStatus(now, addHistory: false);
    }

    public Guid Id { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal TotalAmount { get; private set; }
    public Guid PaidByPersonId { get; private set; }
    public Guid? GroupId { get; private set; }
    public DebtGroup? Group { get; private set; }
    public Person PaidByPerson { get; private set; } = null!;
    public DebtCategory Category { get; private set; }
    public DebtStatus Status { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<DebtShare> Shares => _shares;
    public IReadOnlyCollection<Payment> Payments => _payments;
    public IReadOnlyCollection<DebtHistory> History => _history;

    public void Update(
        string description,
        Guid paidByPersonId,
        DebtCategory category,
        DateOnly? dueDate,
        IReadOnlyCollection<(Guid PersonId, decimal Amount)> shares,
        DateTimeOffset now)
    {
        if (paidByPersonId != PaidByPersonId && _payments.Count > 0)
        {
            throw new InvalidOperationException(
                "The payer cannot be changed after a payment has been recorded.");
        }

        var requestedShares = shares.ToDictionary(share => share.PersonId, share => share.Amount);
        foreach (var existingShare in _shares.ToList())
        {
            if (requestedShares.ContainsKey(existingShare.PersonId))
            {
                continue;
            }

            if (_payments.Any(payment => payment.DebtShareId == existingShare.Id))
            {
                throw new InvalidOperationException(
                    "A participant with payment history cannot be removed from the debt.");
            }

            _shares.Remove(existingShare);
        }

        foreach (var requestedShare in requestedShares)
        {
            var existingShare = _shares.FirstOrDefault(
                share => share.PersonId == requestedShare.Key);
            if (existingShare is null)
            {
                _shares.Add(new DebtShare(
                    requestedShare.Key,
                    requestedShare.Value,
                    requestedShare.Key == paidByPersonId));
                continue;
            }

            var willBePayer = existingShare.PersonId == paidByPersonId;
            if (!willBePayer)
            {
                var pendingAmount = _payments
                    .Where(payment => payment.DebtShareId == existingShare.Id &&
                                      payment.Status == PaymentStatus.Pending)
                    .Sum(payment => payment.Amount);
                var minimumAmount = existingShare.PaidAmount + pendingAmount;
                if (requestedShare.Value < minimumAmount)
                {
                    throw new InvalidOperationException(
                        $"A share cannot be lower than its paid and pending amount of {minimumAmount:F2}.");
                }
            }

            existingShare.Update(
                requestedShare.Value,
                existingShare.PersonId == PaidByPersonId,
                willBePayer);
        }

        Description = description;
        PaidByPersonId = paidByPersonId;
        Category = category;
        DueDate = dueDate;
        UpdatedAt = now;
        _history.Add(new DebtHistory(DebtHistoryType.SplitUpdated, "Debt participants updated.", now));
        RecalculateStatus(now, addHistory: true);
    }

    public Payment AddPayment(
        Guid shareId,
        decimal amount,
        DateOnly paymentDate,
        string? note,
        Guid recordedByUserId,
        Guid? confirmationRequiredFromUserId,
        DateTimeOffset now)
    {
        var share = GetShare(shareId);
        var payment = new Payment(
            shareId,
            amount,
            paymentDate,
            note,
            recordedByUserId,
            confirmationRequiredFromUserId,
            now);
        if (payment.Status == PaymentStatus.Confirmed)
        {
            share.AddPayment(amount);
        }
        _payments.Add(payment);
        UpdatedAt = now;
        _history.Add(new DebtHistory(
            payment.Status == PaymentStatus.Pending
                ? DebtHistoryType.PaymentPending
                : DebtHistoryType.PaymentConfirmed,
            payment.Status == PaymentStatus.Pending
                ? $"Payment of {amount:F2} submitted for confirmation."
                : $"Payment of {amount:F2} confirmed automatically.",
            now));
        RecalculateStatus(now, addHistory: true);
        return payment;
    }

    public void UpdatePayment(
        Guid paymentId,
        Guid userId,
        decimal amount,
        DateOnly paymentDate,
        string? note,
        DateTimeOffset now)
    {
        var payment = GetPayment(paymentId);
        payment.Update(userId, amount, paymentDate, note, now);
        UpdatedAt = now;
        _history.Add(new DebtHistory(
            DebtHistoryType.PaymentUpdated,
            $"Payment updated to {amount:F2}.",
            now));
        RecalculateStatus(now, addHistory: false);
    }

    public void DeletePayment(Guid paymentId, Guid userId, DateTimeOffset now)
    {
        var payment = GetPayment(paymentId);
        payment.EnsureCanDelete(userId);
        if (payment.Status == PaymentStatus.Confirmed)
        {
            GetShare(payment.DebtShareId).RemovePayment(payment.Amount);
        }
        _payments.Remove(payment);
        UpdatedAt = now;
        _history.Add(new DebtHistory(
            DebtHistoryType.PaymentDeleted,
            $"Payment of {payment.Amount:F2} deleted.",
            now));
        RecalculateStatus(now, addHistory: true);
    }

    public void ConfirmPayment(Guid paymentId, Guid userId, DateTimeOffset now)
    {
        var payment = GetPayment(paymentId);
        var share = GetShare(payment.DebtShareId);
        if (payment.Amount > share.RemainingAmount)
        {
            throw new InvalidOperationException("The payment exceeds the remaining share amount.");
        }

        payment.Confirm(userId, now);
        share.AddPayment(payment.Amount);
        UpdatedAt = now;
        _history.Add(new DebtHistory(
            DebtHistoryType.PaymentConfirmed,
            $"Payment of {payment.Amount:F2} confirmed.",
            now));
        RecalculateStatus(now, addHistory: true);
    }

    public void RejectPayment(Guid paymentId, Guid userId, DateTimeOffset now)
    {
        var payment = GetPayment(paymentId);
        payment.Reject(userId, now);
        UpdatedAt = now;
        _history.Add(new DebtHistory(
            DebtHistoryType.PaymentRejected,
            $"Payment of {payment.Amount:F2} rejected.",
            now));
    }

    public void AnonymizeDeletedUser(Guid userId, DateTimeOffset now)
    {
        if (CreatedByUserId == userId)
        {
            CreatedByUserId = Guid.Empty;
        }

        foreach (var payment in _payments)
        {
            payment.AnonymizeDeletedUser(userId, now);
        }

        UpdatedAt = now;
    }

    private DebtShare GetShare(Guid shareId)
    {
        return _shares.FirstOrDefault(share => share.Id == shareId)
            ?? throw new InvalidOperationException("Debt share was not found in the aggregate.");
    }

    private Payment GetPayment(Guid paymentId)
    {
        return _payments.FirstOrDefault(payment => payment.Id == paymentId)
            ?? throw new InvalidOperationException("Payment was not found in the aggregate.");
    }

    private void RecalculateStatus(DateTimeOffset now, bool addHistory)
    {
        var previousStatus = Status;
        Status = _shares
            .Where(share => share.PersonId != PaidByPersonId)
            .All(share => share.RemainingAmount == 0m)
                ? DebtStatus.Paid
                : DebtStatus.Open;

        if (!addHistory || previousStatus == Status)
        {
            return;
        }

        var type = Status == DebtStatus.Paid ? DebtHistoryType.Paid : DebtHistoryType.Reopened;
        var description = Status == DebtStatus.Paid ? "Debt paid." : "Debt reopened.";
        _history.Add(new DebtHistory(type, description, now));
    }
}
