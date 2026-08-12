using FinanceControl.DebtService.Contracts.Debts;
using FinanceControl.DebtService.Domain;
using FinanceControl.DebtService.Errors;
using FinanceControl.DebtService.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.DebtService.Services;

public sealed class DebtManagementService(
    DebtDbContext dbContext,
    SocialConnectionService socialConnectionService,
    GroupService groupService,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<DebtResponse>> FindAllAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var debts = await DebtQuery(userId, tracking: false)
            .OrderByDescending(debt => debt.CreatedAt)
            .ThenBy(debt => debt.Id)
            .ToListAsync(cancellationToken);

        return debts.Select(debt => debt.ToResponse(userId)).ToList();
    }

    public async Task<DebtResponse> FindByIdAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken)
    {
        return (await FindDebtAsync(userId, id, tracking: false, cancellationToken)).ToResponse(userId);
    }

    public async Task<DebtResponse> CreateAsync(
        Guid userId,
        CreateDebtRequest request,
        CancellationToken cancellationToken)
    {
        RequestValidator.Validate(request);
        var personIds = request.Shares
            .Select(share => share.PersonId)
            .Append(request.PaidByPersonId)
            .Distinct()
            .ToList();
        var existingPeople = await dbContext.People
            .Where(person => personIds.Contains(person.Id) &&
                             (person.OwnerUserId == userId || person.LinkedUserId == userId))
            .ToListAsync(cancellationToken);
        var existingPersonIds = existingPeople.Select(person => person.Id).ToList();
        var missingPersonIds = personIds.Except(existingPersonIds).ToList();
        if (missingPersonIds.Count > 0)
        {
            throw DomainValidationException.For(
                "people",
                $"The following people do not exist: {string.Join(", ", missingPersonIds)}.");
        }

        if (request.GroupId is not null &&
            !await groupService.UsersShareGroupAsync(
                request.GroupId.Value,
                userId,
                userId,
                cancellationToken))
        {
            throw DomainValidationException.For("groupId", "The group does not exist or is not accessible.");
        }

        foreach (var participant in existingPeople.Where(person =>
                     person.LinkedUserId is not null && person.LinkedUserId != userId))
        {
            var linkedUserId = participant.LinkedUserId!.Value;
            var isAllowedByGroup = request.GroupId is not null &&
                await groupService.UsersShareGroupAsync(
                    request.GroupId.Value,
                    userId,
                    linkedUserId,
                    cancellationToken);
            if (!isAllowedByGroup &&
                !await socialConnectionService.AreFriendsAsync(userId, linkedUserId, cancellationToken))
            {
                throw DomainValidationException.For(
                    "shares",
                    "Registered users must be accepted friends or members of the selected group.");
            }
        }

        var debt = new Debt(
            userId,
            request.Description.Trim(),
            request.TotalAmount,
            request.PaidByPersonId,
            request.GroupId,
            request.Category,
            request.DueDate,
            request.Shares.Select(share => (share.PersonId, share.Amount)).ToList(),
            timeProvider.GetUtcNow());
        dbContext.Debts.Add(debt);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (await FindDebtAsync(userId, debt.Id, tracking: false, cancellationToken)).ToResponse(userId);
    }

    public async Task<DebtResponse> UpdateAsync(
        Guid userId,
        Guid id,
        UpdateDebtRequest request,
        CancellationToken cancellationToken)
    {
        RequestValidator.Validate(request);
        var debt = await FindOwnedDebtAsync(userId, id, tracking: true, cancellationToken);
        if (request.Shares.Sum(share => share.Amount) != debt.TotalAmount)
        {
            throw DomainValidationException.For(
                "shares",
                $"The sum of all shares must equal the debt total of {debt.TotalAmount:F2}.");
        }

        var personIds = request.Shares
            .Select(share => share.PersonId)
            .Append(request.PaidByPersonId)
            .Distinct()
            .ToList();
        var existingPeople = await dbContext.People
            .Where(person => personIds.Contains(person.Id) && person.OwnerUserId == userId)
            .ToListAsync(cancellationToken);
        var missingPersonIds = personIds
            .Except(existingPeople.Select(person => person.Id))
            .ToList();
        if (missingPersonIds.Count > 0)
        {
            throw DomainValidationException.For(
                "people",
                $"The following people do not exist: {string.Join(", ", missingPersonIds)}.");
        }

        foreach (var participant in existingPeople.Where(person =>
                     person.LinkedUserId is not null && person.LinkedUserId != userId))
        {
            var linkedUserId = participant.LinkedUserId!.Value;
            var isAllowedByGroup = debt.GroupId is not null &&
                await groupService.UsersShareGroupAsync(
                    debt.GroupId.Value,
                    userId,
                    linkedUserId,
                    cancellationToken);
            if (!isAllowedByGroup &&
                !await socialConnectionService.AreFriendsAsync(userId, linkedUserId, cancellationToken))
            {
                throw DomainValidationException.For(
                    "shares",
                    "Registered users must be accepted friends or members of the debt group.");
            }
        }

        debt.Update(
            request.Description.Trim(),
            request.PaidByPersonId,
            request.Category,
            request.DueDate,
            request.Shares.Select(share => (share.PersonId, share.Amount)).ToList(),
            timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await FindDebtAsync(userId, id, tracking: false, cancellationToken)).ToResponse(userId);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var debt = await FindOwnedDebtAsync(userId, id, tracking: true, cancellationToken);
        dbContext.Debts.Remove(debt);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentResponse>> FindPaymentsAsync(
        Guid userId,
        Guid debtId,
        CancellationToken cancellationToken)
    {
        var debt = await FindDebtAsync(userId, debtId, tracking: false, cancellationToken);
        return debt.Payments
            .OrderByDescending(payment => payment.PaymentDate)
            .ThenByDescending(payment => payment.CreatedAt)
            .Select(payment => payment.ToResponse(debt, userId))
            .ToList();
    }

    public async Task<IReadOnlyList<PaymentResponse>> FindPendingConfirmationsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var debts = await DebtQuery(userId, tracking: false)
            .Where(debt => debt.Payments.Any(payment =>
                payment.Status == PaymentStatus.Pending &&
                payment.ConfirmationRequiredFromUserId == userId))
            .ToListAsync(cancellationToken);

        return debts
            .SelectMany(debt => debt.Payments
                .Where(payment =>
                    payment.Status == PaymentStatus.Pending &&
                    payment.ConfirmationRequiredFromUserId == userId)
                .Select(payment => payment.ToResponse(debt, userId)))
            .OrderByDescending(payment => payment.CreatedAt)
            .ThenBy(payment => payment.Id)
            .ToList();
    }

    public async Task<PaymentResponse> AddPaymentAsync(
        Guid userId,
        Guid debtId,
        Guid shareId,
        PaymentRequest request,
        CancellationToken cancellationToken)
    {
        RequestValidator.Validate(request);
        var debt = await FindDebtAsync(userId, debtId, tracking: true, cancellationToken);
        var share = debt.Shares.FirstOrDefault(candidate => candidate.Id == shareId)
            ?? throw new ResourceNotFoundException(
                $"Share with id {shareId} was not found in debt {debtId}.");
        EnsurePayableShare(debt, share);
        var pendingAmount = debt.Payments
            .Where(payment => payment.DebtShareId == shareId && payment.Status == PaymentStatus.Pending)
            .Sum(payment => payment.Amount);
        var availableAmount = share.RemainingAmount - pendingAmount;
        if (request.Amount > availableAmount)
        {
            throw DomainValidationException.For(
                "amount",
                $"Amount cannot exceed the available share amount of {availableAmount:F2}.");
        }

        var confirmationRequiredFromUserId = DetermineConfirmationUser(debt, share, userId);

        var payment = debt.AddPayment(
            shareId,
            request.Amount,
            request.PaymentDate,
            NormalizeNote(request.Note),
            userId,
            confirmationRequiredFromUserId,
            timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        var reloadedDebt = await FindDebtAsync(userId, debtId, tracking: false, cancellationToken);
        return reloadedDebt.Payments.Single(candidate => candidate.Id == payment.Id)
            .ToResponse(reloadedDebt, userId);
    }

    public async Task<PaymentResponse> UpdatePaymentAsync(
        Guid userId,
        Guid debtId,
        Guid paymentId,
        PaymentRequest request,
        CancellationToken cancellationToken)
    {
        RequestValidator.Validate(request);
        var debt = await FindDebtAsync(userId, debtId, tracking: true, cancellationToken);
        var payment = debt.Payments.FirstOrDefault(candidate => candidate.Id == paymentId)
            ?? throw new ResourceNotFoundException(
                $"Payment with id {paymentId} was not found in debt {debtId}.");
        var share = debt.Shares.Single(candidate => candidate.Id == payment.DebtShareId);
        EnsurePayableShare(debt, share);
        var otherPendingAmount = debt.Payments
            .Where(candidate => candidate.Id != paymentId &&
                                candidate.DebtShareId == share.Id &&
                                candidate.Status == PaymentStatus.Pending)
            .Sum(candidate => candidate.Amount);
        var maximumAmount = share.RemainingAmount - otherPendingAmount;
        if (request.Amount > maximumAmount)
        {
            throw DomainValidationException.For(
                "amount",
                $"Amount cannot exceed the available share amount of {maximumAmount:F2}.");
        }

        debt.UpdatePayment(
            paymentId,
            userId,
            request.Amount,
            request.PaymentDate,
            NormalizeNote(request.Note),
            timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        var reloadedDebt = await FindDebtAsync(userId, debtId, tracking: false, cancellationToken);
        return reloadedDebt.Payments.Single(candidate => candidate.Id == paymentId)
            .ToResponse(reloadedDebt, userId);
    }

    public async Task DeletePaymentAsync(
        Guid userId,
        Guid debtId,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var debt = await FindDebtAsync(userId, debtId, tracking: true, cancellationToken);
        if (debt.Payments.All(candidate => candidate.Id != paymentId))
        {
            throw new ResourceNotFoundException(
                $"Payment with id {paymentId} was not found in debt {debtId}.");
        }

        debt.DeletePayment(paymentId, userId, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PaymentResponse> ConfirmPaymentAsync(
        Guid userId,
        Guid debtId,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var debt = await FindDebtAsync(userId, debtId, tracking: true, cancellationToken);
        debt.ConfirmPayment(paymentId, userId, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        var reloadedDebt = await FindDebtAsync(userId, debtId, tracking: false, cancellationToken);
        return reloadedDebt.Payments.Single(payment => payment.Id == paymentId)
            .ToResponse(reloadedDebt, userId);
    }

    public async Task<PaymentResponse> RejectPaymentAsync(
        Guid userId,
        Guid debtId,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var debt = await FindDebtAsync(userId, debtId, tracking: true, cancellationToken);
        debt.RejectPayment(paymentId, userId, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        var reloadedDebt = await FindDebtAsync(userId, debtId, tracking: false, cancellationToken);
        return reloadedDebt.Payments.Single(payment => payment.Id == paymentId)
            .ToResponse(reloadedDebt, userId);
    }

    public async Task<IReadOnlyList<DebtHistoryResponse>> FindHistoryAsync(
        Guid userId,
        Guid debtId,
        CancellationToken cancellationToken)
    {
        var debt = await FindDebtAsync(userId, debtId, tracking: false, cancellationToken);
        return debt.History
            .OrderBy(history => history.OccurredAt)
            .ThenBy(history => history.Type)
            .Select(history => history.ToResponse())
            .ToList();
    }

    private IQueryable<Debt> DebtQuery(Guid userId, bool tracking)
    {
        var query = dbContext.Debts
            .Include(debt => debt.PaidByPerson)
            .Include(debt => debt.Shares)
                .ThenInclude(share => share.Person)
            .Include(debt => debt.Payments)
                .ThenInclude(payment => payment.DebtShare)
                    .ThenInclude(share => share.Person)
            .Include(debt => debt.History)
            .Where(debt => debt.CreatedByUserId == userId ||
                           debt.PaidByPerson.LinkedUserId == userId ||
                           debt.Shares.Any(share => share.Person.LinkedUserId == userId))
            .AsSplitQuery();

        return tracking ? query : query.AsNoTracking();
    }

    private async Task<Debt> FindDebtAsync(
        Guid userId,
        Guid id,
        bool tracking,
        CancellationToken cancellationToken)
    {
        return await DebtQuery(userId, tracking).SingleOrDefaultAsync(debt => debt.Id == id, cancellationToken)
            ?? throw new ResourceNotFoundException($"Debt with id {id} was not found.");
    }

    private async Task<Debt> FindOwnedDebtAsync(
        Guid userId,
        Guid id,
        bool tracking,
        CancellationToken cancellationToken)
    {
        return await DebtQuery(userId, tracking).SingleOrDefaultAsync(
            debt => debt.Id == id && debt.CreatedByUserId == userId,
            cancellationToken)
            ?? throw new ResourceNotFoundException($"Debt with id {id} was not found.");
    }

    private static void EnsurePayableShare(Debt debt, DebtShare share)
    {
        if (share.PersonId == debt.PaidByPersonId)
        {
            throw DomainValidationException.For("shareId", "The payer's own share cannot receive payments.");
        }
    }

    private static Guid? DetermineConfirmationUser(Debt debt, DebtShare share, Guid userId)
    {
        var debtorUserId = share.Person.LinkedUserId;
        var creditorUserId = debt.PaidByPerson.LinkedUserId;
        var registeredParticipants = new[] { debtorUserId, creditorUserId }
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!.Value)
            .Distinct()
            .ToList();

        if (registeredParticipants.Count > 0 && !registeredParticipants.Contains(userId))
        {
            throw new InvalidOperationException(
                "Only the debtor or creditor can record a payment between registered users.");
        }

        if (debtorUserId is not null &&
            creditorUserId is not null &&
            debtorUserId != creditorUserId)
        {
            return userId == debtorUserId ? creditorUserId : debtorUserId;
        }

        if (registeredParticipants.Count == 0 && debt.CreatedByUserId != userId)
        {
            throw new InvalidOperationException("Only the debt creator can record this manual payment.");
        }

        return null;
    }

    private static string? NormalizeNote(string? note)
    {
        return string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
