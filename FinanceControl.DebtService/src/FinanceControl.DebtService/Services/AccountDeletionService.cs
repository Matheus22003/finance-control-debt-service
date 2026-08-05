using FinanceControl.DebtService.Contracts.Users;
using FinanceControl.DebtService.Domain;
using FinanceControl.DebtService.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.DebtService.Services;

public sealed class AccountDeletionService(
    DebtDbContext dbContext,
    TimeProvider timeProvider)
{
    public async Task<AccountDeletionEligibilityResponse> GetEligibilityAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var openDebtsCount = await dbContext.Debts
            .CountAsync(debt => debt.Status == DebtStatus.Open &&
                                (debt.CreatedByUserId == userId ||
                                 debt.PaidByPerson.LinkedUserId == userId ||
                                 debt.Shares.Any(share => share.Person.LinkedUserId == userId)),
                cancellationToken);
        var pendingPaymentsCount = await dbContext.Payments
            .CountAsync(payment => payment.Status == PaymentStatus.Pending &&
                                   (payment.RecordedByUserId == userId ||
                                    payment.ConfirmationRequiredFromUserId == userId),
                cancellationToken);
        var activeSettlementPlansCount = await dbContext.SettlementPlans
            .CountAsync(plan => plan.Status == SettlementPlanStatus.Active &&
                                (plan.CreatedByUserId == userId ||
                                 plan.Transfers.Any(transfer =>
                                     transfer.FromUserId == userId || transfer.ToUserId == userId)),
                cancellationToken);
        var ownedGroupsCount = await dbContext.DebtGroups
            .CountAsync(group => group.CreatedByUserId == userId, cancellationToken);

        var blockers = new List<string>();
        if (openDebtsCount > 0)
            blockers.Add("Quite ou remova todas as dívidas abertas das quais você participa.");
        if (pendingPaymentsCount > 0)
            blockers.Add("Conclua ou rejeite todos os pagamentos pendentes.");
        if (activeSettlementPlansCount > 0)
            blockers.Add("Conclua ou cancele todos os acertos simplificados ativos.");
        if (ownedGroupsCount > 0)
            blockers.Add("Exclua os grupos que você administra antes de excluir a conta.");

        return new AccountDeletionEligibilityResponse(
            blockers.Count == 0,
            openDebtsCount,
            pendingPaymentsCount,
            activeSettlementPlansCount,
            ownedGroupsCount,
            blockers);
    }

    public async Task<AccountDeletionEligibilityResponse> DeleteAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var eligibility = await GetEligibilityAsync(userId, cancellationToken);
        if (!eligibility.CanDelete)
        {
            return eligibility;
        }

        var now = timeProvider.GetUtcNow();
        var debts = await dbContext.Debts
            .Include(debt => debt.Payments)
            .Where(debt => debt.CreatedByUserId == userId ||
                           debt.PaidByPerson.LinkedUserId == userId ||
                           debt.Shares.Any(share => share.Person.LinkedUserId == userId) ||
                           debt.Payments.Any(payment =>
                               payment.RecordedByUserId == userId ||
                               payment.ConfirmationRequiredFromUserId == userId))
            .ToListAsync(cancellationToken);
        foreach (var debt in debts)
        {
            debt.AnonymizeDeletedUser(userId, now);
        }

        var plans = await dbContext.SettlementPlans
            .Include(plan => plan.Transfers)
            .Where(plan => plan.CreatedByUserId == userId ||
                           plan.Transfers.Any(transfer =>
                               transfer.FromUserId == userId ||
                               transfer.ToUserId == userId ||
                               transfer.RecordedByUserId == userId))
            .ToListAsync(cancellationToken);
        foreach (var plan in plans)
        {
            plan.AnonymizeDeletedUser(userId, now);
        }

        var referencedPersonIds = (await dbContext.DebtShares
                .Select(share => share.PersonId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .Concat(await dbContext.Debts
                .Select(debt => debt.PaidByPersonId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var people = await dbContext.People
            .Where(person => person.OwnerUserId == userId || person.LinkedUserId == userId)
            .ToListAsync(cancellationToken);
        var peopleToDelete = people
            .Where(person => person.OwnerUserId == userId && !referencedPersonIds.Contains(person.Id))
            .ToList();
        dbContext.People.RemoveRange(peopleToDelete);
        foreach (var person in people.Except(peopleToDelete))
        {
            person.AnonymizeDeletedUser(userId, now);
        }

        var friendships = await dbContext.Friendships
            .Where(friendship => friendship.RequesterUserId == userId ||
                                 friendship.AddresseeUserId == userId)
            .ToListAsync(cancellationToken);
        dbContext.Friendships.RemoveRange(friendships);

        var memberships = await dbContext.DebtGroupMembers
            .Where(member => member.UserId == userId)
            .ToListAsync(cancellationToken);
        dbContext.DebtGroupMembers.RemoveRange(memberships);

        await dbContext.SaveChangesAsync(cancellationToken);
        return eligibility;
    }
}
