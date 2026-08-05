using FinanceControl.DebtService.Contracts.Debts;
using FinanceControl.DebtService.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.DebtService.Services;

public sealed class DebtSummaryService(DebtDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<DebtSummaryResponse> GetSummaryAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var currentPersonIds = await dbContext.People
            .Where(person => person.LinkedUserId == userId)
            .Select(person => person.Id)
            .ToListAsync(cancellationToken);
        if (currentPersonIds.Count == 0)
        {
            return new DebtSummaryResponse(0m, 0m, 0);
        }

        var debts = await dbContext.Debts
            .AsNoTracking()
            .Include(debt => debt.Shares)
            .Where(debt => debt.CreatedByUserId == userId ||
                           debt.PaidByPerson.LinkedUserId == userId ||
                           debt.Shares.Any(share => share.Person.LinkedUserId == userId))
            .ToListAsync(cancellationToken);
        var totalOwed = debts
            .Where(debt => !currentPersonIds.Contains(debt.PaidByPersonId))
            .SelectMany(debt => debt.Shares)
            .Where(share => currentPersonIds.Contains(share.PersonId))
            .Sum(share => share.RemainingAmount);
        var totalToReceive = debts
            .Where(debt => currentPersonIds.Contains(debt.PaidByPersonId))
            .SelectMany(debt => debt.Shares)
            .Where(share => !currentPersonIds.Contains(share.PersonId))
            .Sum(share => share.RemainingAmount);
        var openDebtsCount = debts.Count(debt =>
            debt.Shares.Any(share =>
                share.RemainingAmount > 0m &&
                ((currentPersonIds.Contains(debt.PaidByPersonId) &&
                  !currentPersonIds.Contains(share.PersonId)) ||
                 (!currentPersonIds.Contains(debt.PaidByPersonId) &&
                  currentPersonIds.Contains(share.PersonId)))));

        return new DebtSummaryResponse(totalOwed, totalToReceive, openDebtsCount);
    }

    public async Task<DebtAnalysisContextResponse> GetAnalysisContextAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var dueSoonLimit = today.AddDays(7);
        var currentPersonIds = (await dbContext.People
                .Where(person => person.LinkedUserId == userId)
                .Select(person => person.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        if (currentPersonIds.Count == 0)
        {
            return new DebtAnalysisContextResponse(
                now, 0m, 0m, 0, 0, 0, 0, [], [], []);
        }

        var debts = await dbContext.Debts
            .AsNoTracking()
            .Include(debt => debt.Group)
            .Include(debt => debt.Shares)
            .Where(debt => debt.PaidByPerson.LinkedUserId == userId ||
                           debt.Shares.Any(share => share.Person.LinkedUserId == userId))
            .ToListAsync(cancellationToken);

        var positions = debts
            .Select(debt =>
            {
                var userIsPayer = currentPersonIds.Contains(debt.PaidByPersonId);
                var totalOwed = userIsPayer
                    ? 0m
                    : debt.Shares
                        .Where(share => currentPersonIds.Contains(share.PersonId))
                        .Sum(share => share.RemainingAmount);
                var totalToReceive = userIsPayer
                    ? debt.Shares
                        .Where(share => !currentPersonIds.Contains(share.PersonId))
                        .Sum(share => share.RemainingAmount)
                    : 0m;

                return new DebtPosition(
                    debt.Category.ToString().ToUpperInvariant(),
                    debt.GroupId,
                    debt.Group?.Name,
                    debt.DueDate,
                    debt.Status == Domain.DebtStatus.Paid,
                    totalOwed,
                    totalToReceive);
            })
            .Where(position => position.TotalOwed > 0m ||
                               position.TotalToReceive > 0m ||
                               position.IsPaid)
            .ToList();

        var openPositions = positions
            .Where(position => position.TotalOwed > 0m || position.TotalToReceive > 0m)
            .ToList();

        var categories = openPositions
            .GroupBy(position => position.Category)
            .Select(group => new DebtAnalysisCategoryResponse(
                group.Key,
                group.Sum(position => position.TotalOwed),
                group.Sum(position => position.TotalToReceive),
                group.Count()))
            .OrderByDescending(category => category.TotalOwed + category.TotalToReceive)
            .ToList();

        var groups = openPositions
            .GroupBy(position => new { position.GroupId, position.GroupName })
            .Select(group => new DebtAnalysisGroupResponse(
                group.Key.GroupId,
                group.Key.GroupName,
                group.Sum(position => position.TotalOwed),
                group.Sum(position => position.TotalToReceive),
                group.Count()))
            .OrderByDescending(group => group.TotalOwed + group.TotalToReceive)
            .ToList();

        var topDrivers = openPositions
            .OrderByDescending(position => position.TotalOwed + position.TotalToReceive)
            .Take(5)
            .Select(position => new DebtAnalysisDriverResponse(
                position.Category,
                position.GroupId,
                position.GroupName,
                position.TotalOwed,
                position.TotalToReceive,
                position.DueDate,
                position.TotalOwed > 0m && position.DueDate < today))
            .ToList();

        return new DebtAnalysisContextResponse(
            now,
            openPositions.Sum(position => position.TotalOwed),
            openPositions.Sum(position => position.TotalToReceive),
            openPositions.Count,
            positions.Count(position => position.IsPaid),
            openPositions.Count(position =>
                position.TotalOwed > 0m && position.DueDate < today),
            openPositions.Count(position =>
                position.TotalOwed > 0m &&
                position.DueDate >= today &&
                position.DueDate <= dueSoonLimit),
            categories,
            groups,
            topDrivers);
    }

    private sealed record DebtPosition(
        string Category,
        Guid? GroupId,
        string? GroupName,
        DateOnly? DueDate,
        bool IsPaid,
        decimal TotalOwed,
        decimal TotalToReceive);
}
