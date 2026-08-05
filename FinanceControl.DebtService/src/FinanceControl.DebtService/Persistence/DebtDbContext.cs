using FinanceControl.DebtService.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.DebtService.Persistence;

public sealed class DebtDbContext(DbContextOptions<DebtDbContext> options) : DbContext(options)
{
    public DbSet<Person> People => Set<Person>();
    public DbSet<Debt> Debts => Set<Debt>();
    public DbSet<DebtShare> DebtShares => Set<DebtShare>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<DebtHistory> DebtHistory => Set<DebtHistory>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<DebtGroup> DebtGroups => Set<DebtGroup>();
    public DbSet<DebtGroupMember> DebtGroupMembers => Set<DebtGroupMember>();
    public DbSet<SettlementPlan> SettlementPlans => Set<SettlementPlan>();
    public DbSet<SettlementTransfer> SettlementTransfers => Set<SettlementTransfer>();
    public DbSet<SettlementAllocation> SettlementAllocations => Set<SettlementAllocation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DebtDbContext).Assembly);
    }
}
