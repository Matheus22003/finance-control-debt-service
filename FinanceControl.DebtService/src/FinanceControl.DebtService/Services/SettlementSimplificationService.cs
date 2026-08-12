using FinanceControl.DebtService.Contracts.Debts;
using FinanceControl.DebtService.Domain;
using FinanceControl.DebtService.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.DebtService.Services;

public sealed class SettlementSimplificationService(
    DebtDbContext dbContext,
    GroupService groupService)
{
    public async Task<SimplifiedSettlementResponse> CalculateAsync(
        Guid userId,
        Guid? groupId,
        CancellationToken cancellationToken)
    {
        var snapshot = await BuildSnapshotAsync(userId, groupId, cancellationToken);
        return new SimplifiedSettlementResponse(
            snapshot.TotalOpenAmount,
            snapshot.OriginalTransferCount,
            snapshot.Transfers.Count,
            snapshot.Transfers
                .Select(transfer => new SimplifiedTransferResponse(
                    transfer.FromIdentityId,
                    transfer.FromPerson.ToReference(userId),
                    transfer.ToIdentityId,
                    transfer.ToPerson.ToReference(userId),
                    transfer.Amount))
                .ToList());
    }

    internal async Task<SettlementSnapshot> BuildSnapshotAsync(
        Guid userId,
        Guid? groupId,
        CancellationToken cancellationToken)
    {
        if (groupId is not null)
        {
            await groupService.GetByIdAsync(userId, groupId.Value, cancellationToken);
        }

        var debts = await dbContext.Debts
            .AsNoTracking()
            .Include(debt => debt.PaidByPerson)
            .Include(debt => debt.Shares)
                .ThenInclude(share => share.Person)
            .Where(debt => debt.GroupId == groupId &&
                           (debt.CreatedByUserId == userId ||
                            debt.PaidByPerson.LinkedUserId == userId ||
                            debt.Shares.Any(share => share.Person.LinkedUserId == userId)))
            .ToListAsync(cancellationToken);
        var balances = new Dictionary<Guid, decimal>();
        var people = new Dictionary<Guid, Person>();
        var allocations = new List<SettlementSnapshotAllocation>();
        var originalTransferCount = 0;
        var totalOpenAmount = 0m;

        foreach (var debt in debts)
        {
            var payerIdentity = GetIdentity(debt.PaidByPerson);
            RegisterPerson(payerIdentity, debt.PaidByPerson);
            foreach (var share in debt.Shares.Where(share => share.RemainingAmount > 0m))
            {
                var debtorIdentity = GetIdentity(share.Person);
                RegisterPerson(debtorIdentity, share.Person);
                if (debtorIdentity == payerIdentity)
                {
                    continue;
                }

                balances[debtorIdentity] =
                    balances.GetValueOrDefault(debtorIdentity) - share.RemainingAmount;
                balances[payerIdentity] =
                    balances.GetValueOrDefault(payerIdentity) + share.RemainingAmount;
                allocations.Add(new SettlementSnapshotAllocation(
                    debt.Id,
                    share.Id,
                    share.RemainingAmount));
                originalTransferCount++;
                totalOpenAmount += share.RemainingAmount;
            }
        }

        var debtors = balances
            .Where(balance => balance.Value < 0m)
            .Select(balance => new Balance(balance.Key, -balance.Value))
            .OrderByDescending(balance => balance.Amount)
            .ThenBy(balance => people[balance.PersonId].Name)
            .ToList();
        var creditors = balances
            .Where(balance => balance.Value > 0m)
            .Select(balance => new Balance(balance.Key, balance.Value))
            .OrderByDescending(balance => balance.Amount)
            .ThenBy(balance => people[balance.PersonId].Name)
            .ToList();
        var transfers = CalculateMinimumTransfers(debtors, creditors, cancellationToken)
            .Select(transfer => new SettlementSnapshotTransfer(
                transfer.FromPersonId,
                people[transfer.FromPersonId],
                transfer.ToPersonId,
                people[transfer.ToPersonId],
                transfer.Amount))
            .ToList();

        return new SettlementSnapshot(
            totalOpenAmount,
            originalTransferCount,
            transfers,
            allocations);

        void RegisterPerson(Guid identity, Person person)
        {
            if (!people.TryGetValue(identity, out var current) ||
                (current.OwnerUserId != userId && person.OwnerUserId == userId))
            {
                people[identity] = person;
            }
        }
    }

    internal static Guid GetIdentity(Person person) => person.LinkedUserId ?? person.Id;

    private static IReadOnlyList<Transfer> CalculateMinimumTransfers(
        IReadOnlyList<Balance> debtors,
        IReadOnlyList<Balance> creditors,
        CancellationToken cancellationToken)
    {
        if (debtors.Count == 0 || creditors.Count == 0)
        {
            return [];
        }

        var debtorAmounts = debtors.Select(debtor => debtor.Amount).ToArray();
        var creditorAmounts = creditors.Select(creditor => creditor.Amount).ToArray();
        List<Transfer>? best = null;
        var current = new List<Transfer>();
        var visited = new Dictionary<string, int>(StringComparer.Ordinal);

        Search();
        return best ?? [];

        void Search()
        {
            cancellationToken.ThrowIfCancellationRequested();
            var debtorIndex = Array.FindIndex(debtorAmounts, amount => amount > 0m);
            if (debtorIndex < 0)
            {
                if (best is null || current.Count < best.Count)
                {
                    best = [.. current];
                }

                return;
            }

            var remainingDebtors = debtorAmounts.Count(amount => amount > 0m);
            var remainingCreditors = creditorAmounts.Count(amount => amount > 0m);
            var lowerBound = Math.Max(remainingDebtors, remainingCreditors);
            if (best is not null && current.Count + lowerBound >= best.Count)
            {
                return;
            }

            var state = string.Join('|', debtorAmounts) + ":" + string.Join('|', creditorAmounts);
            if (visited.TryGetValue(state, out var previousDepth) && previousDepth <= current.Count)
            {
                return;
            }

            visited[state] = current.Count;
            var triedCreditorAmounts = new HashSet<decimal>();
            for (var creditorIndex = 0; creditorIndex < creditorAmounts.Length; creditorIndex++)
            {
                if (creditorAmounts[creditorIndex] <= 0m ||
                    !triedCreditorAmounts.Add(creditorAmounts[creditorIndex]))
                {
                    continue;
                }

                var amount = Math.Min(debtorAmounts[debtorIndex], creditorAmounts[creditorIndex]);
                debtorAmounts[debtorIndex] -= amount;
                creditorAmounts[creditorIndex] -= amount;
                current.Add(new Transfer(
                    debtors[debtorIndex].PersonId,
                    creditors[creditorIndex].PersonId,
                    amount));

                Search();

                current.RemoveAt(current.Count - 1);
                debtorAmounts[debtorIndex] += amount;
                creditorAmounts[creditorIndex] += amount;
            }
        }
    }

    private sealed record Balance(Guid PersonId, decimal Amount);

    private sealed record Transfer(Guid FromPersonId, Guid ToPersonId, decimal Amount);
}

internal sealed record SettlementSnapshot(
    decimal TotalOpenAmount,
    int OriginalTransferCount,
    IReadOnlyList<SettlementSnapshotTransfer> Transfers,
    IReadOnlyList<SettlementSnapshotAllocation> Allocations);

internal sealed record SettlementSnapshotTransfer(
    Guid FromIdentityId,
    Person FromPerson,
    Guid ToIdentityId,
    Person ToPerson,
    decimal Amount);

internal sealed record SettlementSnapshotAllocation(
    Guid DebtId,
    Guid DebtShareId,
    decimal Amount);
