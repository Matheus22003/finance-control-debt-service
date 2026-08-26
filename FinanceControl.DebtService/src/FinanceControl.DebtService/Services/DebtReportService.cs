using FinanceControl.DebtService.Contracts.Debts;
using FinanceControl.DebtService.Domain;
using FinanceControl.DebtService.Errors;
using FinanceControl.DebtService.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.DebtService.Services;

public sealed class DebtReportService(DebtDbContext dbContext)
{
    public async Task<DebtReportResponse> GetOverviewAsync(
        Guid userId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        ValidateRange(fromDate, toDate);
        var currentPersonIds = (await dbContext.People
                .Where(person => person.LinkedUserId == userId)
                .Select(person => person.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var fromInstant = new DateTimeOffset(
            fromDate.ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero);
        var toExclusiveInstant = new DateTimeOffset(
            toDate.AddDays(1).ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero);

        var debts = await dbContext.Debts
            .AsNoTracking()
            .Include(debt => debt.Shares)
            .Where(debt => debt.CreatedAt >= fromInstant &&
                           debt.CreatedAt < toExclusiveInstant &&
                           (debt.CreatedByUserId == userId ||
                            debt.PaidByPerson.LinkedUserId == userId ||
                            debt.Shares.Any(share => share.Person.LinkedUserId == userId)))
            .ToListAsync(cancellationToken);

        var positions = debts.Select(debt => BuildPosition(debt, currentPersonIds)).ToList();
        var months = positions
            .GroupBy(position => $"{position.Debt.CreatedAt.Year:D4}-{position.Debt.CreatedAt.Month:D2}")
            .OrderBy(group => group.Key)
            .Select(group => new DebtReportMonthResponse(
                group.Key,
                group.Sum(position => position.Debt.TotalAmount),
                group.Sum(position => position.TotalOwed),
                group.Sum(position => position.TotalToReceive),
                group.Count()))
            .ToList();
        var categories = positions
            .GroupBy(position => position.Debt.Category.ToString().ToUpperInvariant())
            .Select(group => new DebtReportCategoryResponse(
                group.Key,
                group.Sum(position => position.Debt.TotalAmount),
                group.Sum(position => position.TotalOwed),
                group.Sum(position => position.TotalToReceive),
                group.Count()))
            .OrderByDescending(category => category.TotalOwed + category.TotalToReceive)
            .ThenByDescending(category => category.TotalVolume)
            .ToList();
        var topDebts = positions
            .OrderByDescending(position => position.TotalOwed + position.TotalToReceive)
            .ThenByDescending(position => position.Debt.TotalAmount)
            .Take(5)
            .Select(position => new DebtReportItemResponse(
                position.Debt.Id,
                position.Debt.Description,
                position.Debt.Category.ToString().ToUpperInvariant(),
                position.Debt.TotalAmount,
                position.TotalOwed,
                position.TotalToReceive,
                position.Debt.Status.ToString().ToUpperInvariant(),
                position.Debt.DueDate,
                position.Debt.CreatedAt))
            .ToList();

        return new DebtReportResponse(
            fromDate,
            toDate,
            positions.Sum(position => position.Debt.TotalAmount),
            positions.Sum(position => position.TotalOwed),
            positions.Sum(position => position.TotalToReceive),
            positions.Count(position => position.Debt.Status == DebtStatus.Open),
            positions.Count(position => position.Debt.Status == DebtStatus.Paid),
            months,
            categories,
            topDebts);
    }

    private static DebtPosition BuildPosition(Debt debt, HashSet<Guid> currentPersonIds)
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
        return new DebtPosition(debt, totalOwed, totalToReceive);
    }

    private static void ValidateRange(DateOnly fromDate, DateOnly toDate)
    {
        if (fromDate > toDate)
        {
            throw DomainValidationException.For("period", "From date must be on or before to date.");
        }

        if (fromDate.AddMonths(24).AddDays(-1) < toDate)
        {
            throw DomainValidationException.For("period", "Reports support a maximum period of 24 months.");
        }
    }

    private sealed record DebtPosition(Debt Debt, decimal TotalOwed, decimal TotalToReceive);
}
