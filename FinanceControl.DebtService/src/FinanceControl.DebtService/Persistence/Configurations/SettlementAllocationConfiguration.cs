using FinanceControl.DebtService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceControl.DebtService.Persistence.Configurations;

internal sealed class SettlementAllocationConfiguration : IEntityTypeConfiguration<SettlementAllocation>
{
    public void Configure(EntityTypeBuilder<SettlementAllocation> builder)
    {
        builder.ToTable("settlement_allocations", tableBuilder =>
            tableBuilder.HasCheckConstraint("ck_settlement_allocations_amount_positive", "amount > 0"));
        builder.HasKey(allocation => allocation.Id);
        builder.Property(allocation => allocation.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(allocation => allocation.SettlementPlanId)
            .HasColumnName("settlement_plan_id")
            .IsRequired();
        builder.Property(allocation => allocation.DebtId).HasColumnName("debt_id").IsRequired();
        builder.Property(allocation => allocation.DebtShareId).HasColumnName("debt_share_id").IsRequired();
        builder.Property(allocation => allocation.Amount)
            .HasColumnName("amount")
            .HasPrecision(19, 2)
            .IsRequired();
        builder.HasIndex(allocation => allocation.DebtId)
            .HasDatabaseName("ix_settlement_allocations_debt_id");
        builder.HasIndex(allocation => allocation.DebtShareId)
            .HasDatabaseName("ix_settlement_allocations_debt_share_id");
    }
}
