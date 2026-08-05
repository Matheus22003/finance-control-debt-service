using FinanceControl.DebtService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceControl.DebtService.Persistence.Configurations;

internal sealed class DebtConfiguration : IEntityTypeConfiguration<Debt>
{
    public void Configure(EntityTypeBuilder<Debt> builder)
    {
        builder.ToTable("debts");
        builder.HasKey(debt => debt.Id);
        builder.Property(debt => debt.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(debt => debt.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(debt => debt.Description).HasColumnName("description").HasMaxLength(200).IsRequired();
        builder.Property(debt => debt.TotalAmount).HasColumnName("total_amount").HasPrecision(19, 2).IsRequired();
        builder.Property(debt => debt.PaidByPersonId).HasColumnName("paid_by_person_id").IsRequired();
        builder.Property(debt => debt.GroupId).HasColumnName("group_id");
        builder.Property(debt => debt.Category)
            .HasColumnName("category")
            .HasConversion(
                category => category.ToString().ToUpperInvariant(),
                value => Enum.Parse<DebtCategory>(value, ignoreCase: true))
            .HasMaxLength(20);
        builder.Property(debt => debt.Status)
            .HasColumnName("status")
            .HasConversion(
                status => status.ToString().ToUpperInvariant(),
                value => Enum.Parse<DebtStatus>(value, ignoreCase: true))
            .HasMaxLength(20);
        builder.Property(debt => debt.DueDate).HasColumnName("due_date");
        builder.Property(debt => debt.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(debt => debt.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne(debt => debt.PaidByPerson)
            .WithMany()
            .HasForeignKey(debt => debt.PaidByPersonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(debt => debt.Group)
            .WithMany()
            .HasForeignKey(debt => debt.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(debt => debt.Shares)
            .WithOne()
            .HasForeignKey(share => share.DebtId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(debt => debt.Shares).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(debt => debt.Payments)
            .WithOne()
            .HasForeignKey(payment => payment.DebtId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(debt => debt.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(debt => debt.History)
            .WithOne()
            .HasForeignKey(history => history.DebtId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(debt => debt.History).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(debt => debt.PaidByPersonId).HasDatabaseName("ix_debts_paid_by_person_id");
        builder.HasIndex(debt => debt.CreatedByUserId).HasDatabaseName("ix_debts_created_by_user_id");
        builder.HasIndex(debt => debt.GroupId).HasDatabaseName("ix_debts_group_id");
        builder.HasIndex(debt => debt.Status).HasDatabaseName("ix_debts_status");
        builder.HasIndex(debt => debt.DueDate).HasDatabaseName("ix_debts_due_date");
    }
}
