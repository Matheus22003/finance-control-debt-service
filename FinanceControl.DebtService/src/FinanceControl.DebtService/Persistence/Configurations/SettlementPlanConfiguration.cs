using FinanceControl.DebtService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceControl.DebtService.Persistence.Configurations;

internal sealed class SettlementPlanConfiguration : IEntityTypeConfiguration<SettlementPlan>
{
    public void Configure(EntityTypeBuilder<SettlementPlan> builder)
    {
        builder.ToTable("settlement_plans");
        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(plan => plan.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(plan => plan.GroupId).HasColumnName("group_id");
        builder.Property(plan => plan.Status)
            .HasColumnName("status")
            .HasConversion(
                status => status.ToString().ToUpperInvariant(),
                value => Enum.Parse<SettlementPlanStatus>(value, ignoreCase: true))
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(plan => plan.CompletedAt).HasColumnName("completed_at");
        builder.Property(plan => plan.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(plan => plan.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(plan => plan.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasMany(plan => plan.Transfers)
            .WithOne()
            .HasForeignKey(transfer => transfer.SettlementPlanId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(plan => plan.Transfers).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(plan => plan.Allocations)
            .WithOne()
            .HasForeignKey(allocation => allocation.SettlementPlanId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(plan => plan.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(plan => new { plan.GroupId, plan.Status })
            .HasDatabaseName("ix_settlement_plans_group_status");
        builder.HasIndex(plan => plan.CreatedByUserId)
            .HasDatabaseName("ix_settlement_plans_created_by_user_id");
    }
}
