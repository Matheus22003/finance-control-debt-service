using FinanceControl.DebtService.Contracts.Debts;
using FinanceControl.DebtService.Domain;
using FinanceControl.DebtService.Errors;
using FinanceControl.DebtService.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.DebtService.Services;

public sealed class SettlementTransferService(
    DebtDbContext dbContext,
    GroupService groupService,
    SettlementSimplificationService simplificationService,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<SettlementTransferResponse>> FindActiveAsync(
        Guid userId,
        Guid? groupId,
        CancellationToken cancellationToken)
    {
        if (groupId is not null)
        {
            await groupService.GetByIdAsync(userId, groupId.Value, cancellationToken);
        }

        var plan = await PlanQuery(tracking: false)
            .Where(candidate => candidate.Status == SettlementPlanStatus.Active &&
                                candidate.GroupId == groupId &&
                                (candidate.CreatedByUserId == userId ||
                                 candidate.Transfers.Any(transfer =>
                                     transfer.FromUserId == userId || transfer.ToUserId == userId)))
            .OrderByDescending(candidate => candidate.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return plan is null
            ? []
            : plan.Transfers
                .OrderBy(transfer => transfer.FromPersonName)
                .ThenBy(transfer => transfer.ToPersonName)
                .Select(transfer => ToResponse(transfer, plan, userId))
                .ToList();
    }

    public async Task<IReadOnlyList<SettlementTransferResponse>> FindPendingConfirmationsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var plans = await PlanQuery(tracking: false)
            .Where(plan => plan.Status == SettlementPlanStatus.Active &&
                           plan.Transfers.Any(transfer =>
                               transfer.Status == SettlementTransferStatus.Pending &&
                               transfer.ToUserId == userId))
            .ToListAsync(cancellationToken);
        return plans
            .SelectMany(plan => plan.Transfers
                .Where(transfer => transfer.Status == SettlementTransferStatus.Pending &&
                                   transfer.ToUserId == userId)
                .Select(transfer => ToResponse(transfer, plan, userId)))
            .OrderByDescending(transfer => transfer.UpdatedAt)
            .ThenBy(transfer => transfer.Id)
            .ToList();
    }

    public async Task<SettlementTransferResponse> RecordAsync(
        Guid userId,
        RecordSettlementTransferRequest request,
        CancellationToken cancellationToken)
    {
        RequestValidator.Validate(request);
        var people = await dbContext.People
            .AsNoTracking()
            .Where(person => person.Id == request.FromPersonId || person.Id == request.ToPersonId)
            .ToListAsync(cancellationToken);
        var fromPerson = people.SingleOrDefault(person => person.Id == request.FromPersonId)
            ?? throw new ResourceNotFoundException("The suggested payer was not found.");
        var toPerson = people.SingleOrDefault(person => person.Id == request.ToPersonId)
            ?? throw new ResourceNotFoundException("The suggested recipient was not found.");
        if (fromPerson.LinkedUserId != userId)
        {
            throw new InvalidOperationException("Only the suggested payer can record this transfer.");
        }

        var fromIdentityId = SettlementSimplificationService.GetIdentity(fromPerson);
        var toIdentityId = SettlementSimplificationService.GetIdentity(toPerson);
        var plan = await PlanQuery(tracking: true)
            .Where(candidate => candidate.Status == SettlementPlanStatus.Active &&
                                candidate.GroupId == request.GroupId &&
                                (candidate.CreatedByUserId == userId ||
                                 candidate.Transfers.Any(transfer =>
                                     transfer.FromUserId == userId || transfer.ToUserId == userId)))
            .OrderByDescending(candidate => candidate.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (plan is null)
        {
            plan = await CreatePlanAsync(userId, request.GroupId, cancellationToken);
            dbContext.SettlementPlans.Add(plan);
        }

        plan.EnsureActive();
        var transfer = plan.Transfers.SingleOrDefault(candidate =>
            candidate.FromIdentityId == fromIdentityId &&
            candidate.ToIdentityId == toIdentityId &&
            candidate.Amount == request.Amount)
            ?? throw DomainValidationException.For(
                "transfer",
                "The requested transfer is not part of the current simplified plan.");
        transfer.Record(
            userId,
            request.PaymentDate,
            NormalizeNote(request.Note),
            timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(transfer, plan, userId);
    }

    public async Task<SettlementTransferResponse> ConfirmAsync(
        Guid userId,
        Guid transferId,
        CancellationToken cancellationToken)
    {
        var plan = await FindPlanByTransferAsync(userId, transferId, cancellationToken);
        var transfer = plan.FindTransfer(transferId);
        var now = timeProvider.GetUtcNow();
        transfer.Confirm(userId, now);

        if (plan.Transfers.All(candidate => candidate.Status == SettlementTransferStatus.Confirmed))
        {
            var debts = await LoadAllocatedDebtsAsync(plan, cancellationToken);
            if (!AllocationsAreCurrent(plan, debts))
            {
                plan.Cancel(now);
                await dbContext.SaveChangesAsync(cancellationToken);
                throw new InvalidOperationException(
                    "The debts changed after this plan was created. Recalculate the simplified transfers.");
            }

            var settlementDate = plan.Transfers.Max(candidate => candidate.PaymentDate)!.Value;
            foreach (var allocation in plan.Allocations)
            {
                var debt = debts.Single(candidate => candidate.Id == allocation.DebtId);
                debt.AddPayment(
                    allocation.DebtShareId,
                    allocation.Amount,
                    settlementDate,
                    $"Simplified settlement plan {plan.Id}.",
                    plan.CreatedByUserId,
                    confirmationRequiredFromUserId: null,
                    now);
            }

            plan.Complete(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(transfer, plan, userId);
    }

    public async Task<SettlementTransferResponse> RejectAsync(
        Guid userId,
        Guid transferId,
        CancellationToken cancellationToken)
    {
        var plan = await FindPlanByTransferAsync(userId, transferId, cancellationToken);
        var transfer = plan.FindTransfer(transferId);
        var now = timeProvider.GetUtcNow();
        transfer.Reject(userId, now);
        plan.Cancel(now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(transfer, plan, userId);
    }

    private async Task<SettlementPlan> CreatePlanAsync(
        Guid userId,
        Guid? groupId,
        CancellationToken cancellationToken)
    {
        var snapshot = await simplificationService.BuildSnapshotAsync(
            userId,
            groupId,
            cancellationToken);
        if (snapshot.Transfers.Count == 0)
        {
            throw DomainValidationException.For(
                "transfer",
                "There are no simplified transfers to record in this context.");
        }

        return new SettlementPlan(
            userId,
            groupId,
            snapshot.Transfers.Select(transfer => new SettlementTransferDraft(
                transfer.FromIdentityId,
                transfer.FromPerson.LinkedUserId,
                transfer.FromPerson.Id,
                transfer.FromPerson.Name,
                transfer.ToIdentityId,
                transfer.ToPerson.LinkedUserId,
                transfer.ToPerson.Id,
                transfer.ToPerson.Name,
                transfer.Amount)).ToList(),
            snapshot.Allocations.Select(allocation => new SettlementAllocationDraft(
                allocation.DebtId,
                allocation.DebtShareId,
                allocation.Amount)).ToList(),
            timeProvider.GetUtcNow());
    }

    private async Task<SettlementPlan> FindPlanByTransferAsync(
        Guid userId,
        Guid transferId,
        CancellationToken cancellationToken) =>
        await PlanQuery(tracking: true)
            .SingleOrDefaultAsync(plan =>
                plan.Status == SettlementPlanStatus.Active &&
                plan.Transfers.Any(transfer => transfer.Id == transferId &&
                                               (transfer.FromUserId == userId ||
                                                transfer.ToUserId == userId)),
                cancellationToken)
        ?? throw new ResourceNotFoundException("The active settlement transfer was not found.");

    private async Task<List<Debt>> LoadAllocatedDebtsAsync(
        SettlementPlan plan,
        CancellationToken cancellationToken)
    {
        var debtIds = plan.Allocations.Select(allocation => allocation.DebtId).Distinct().ToList();
        return await dbContext.Debts
            .Include(debt => debt.Shares)
            .Include(debt => debt.Payments)
            .Include(debt => debt.History)
            .Where(debt => debtIds.Contains(debt.Id))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    private static bool AllocationsAreCurrent(
        SettlementPlan plan,
        IReadOnlyCollection<Debt> debts) =>
        plan.Allocations.All(allocation =>
        {
            var debt = debts.SingleOrDefault(candidate => candidate.Id == allocation.DebtId);
            var share = debt?.Shares.SingleOrDefault(candidate => candidate.Id == allocation.DebtShareId);
            return share?.RemainingAmount == allocation.Amount;
        });

    private IQueryable<SettlementPlan> PlanQuery(bool tracking)
    {
        var query = dbContext.SettlementPlans
            .Include(plan => plan.Transfers)
            .Include(plan => plan.Allocations)
            .AsSplitQuery();
        return tracking ? query : query.AsNoTracking();
    }

    private static SettlementTransferResponse ToResponse(
        SettlementTransfer transfer,
        SettlementPlan plan,
        Guid userId) =>
        new(
            transfer.Id,
            plan.Id,
            plan.GroupId,
            transfer.FromIdentityId,
            new PersonReferenceResponse(
                transfer.FromPersonId,
                transfer.FromPersonName,
                transfer.FromUserId == userId),
            transfer.ToIdentityId,
            new PersonReferenceResponse(
                transfer.ToPersonId,
                transfer.ToPersonName,
                transfer.ToUserId == userId),
            transfer.Amount,
            transfer.PaymentDate,
            transfer.Note,
            transfer.Status,
            plan.Status == SettlementPlanStatus.Active &&
                transfer.Status == SettlementTransferStatus.AwaitingPayment &&
                transfer.FromUserId == userId,
            plan.Status == SettlementPlanStatus.Active &&
                transfer.Status == SettlementTransferStatus.Pending &&
                transfer.ToUserId == userId,
            plan.Status == SettlementPlanStatus.Active &&
                transfer.Status == SettlementTransferStatus.Pending &&
                transfer.ToUserId == userId,
            transfer.ConfirmedAt,
            transfer.RejectedAt,
            transfer.CreatedAt,
            transfer.UpdatedAt);

    private static string? NormalizeNote(string? note) =>
        string.IsNullOrWhiteSpace(note) ? null : note.Trim();
}
