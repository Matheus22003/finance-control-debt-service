using FinanceControl.DebtService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceControl.DebtService.Persistence.Configurations;

internal sealed class DebtShareConfiguration : IEntityTypeConfiguration<DebtShare>
{
    public void Configure(EntityTypeBuilder<DebtShare> builder)
    {
        builder.ToTable("debt_shares", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("ck_debt_shares_amount_positive", "amount > 0");
            tableBuilder.HasCheckConstraint(
                "ck_debt_shares_paid_amount_range",
                "paid_amount >= 0 AND paid_amount <= amount");
        });
        builder.HasKey(share => share.Id);
        builder.Property(share => share.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(share => share.DebtId).HasColumnName("debt_id").IsRequired();
        builder.Property(share => share.PersonId).HasColumnName("person_id").IsRequired();
        builder.Property(share => share.Amount).HasColumnName("amount").HasPrecision(19, 2).IsRequired();
        builder.Property(share => share.PaidAmount).HasColumnName("paid_amount").HasPrecision(19, 2).IsRequired();
        builder.Ignore(share => share.RemainingAmount);
        builder.HasOne(share => share.Person)
            .WithMany()
            .HasForeignKey(share => share.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(share => new { share.DebtId, share.PersonId })
            .HasDatabaseName("ux_debt_shares_debt_person")
            .IsUnique();
        builder.HasIndex(share => share.PersonId).HasDatabaseName("ix_debt_shares_person_id");
    }
}
