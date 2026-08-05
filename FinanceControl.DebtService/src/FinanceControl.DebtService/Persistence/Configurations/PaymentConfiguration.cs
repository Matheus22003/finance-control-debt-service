using FinanceControl.DebtService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceControl.DebtService.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments", tableBuilder =>
            tableBuilder.HasCheckConstraint("ck_payments_amount_positive", "amount > 0"));
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(payment => payment.DebtId).HasColumnName("debt_id").IsRequired();
        builder.Property(payment => payment.DebtShareId).HasColumnName("debt_share_id").IsRequired();
        builder.Property(payment => payment.Amount).HasColumnName("amount").HasPrecision(19, 2).IsRequired();
        builder.Property(payment => payment.PaymentDate).HasColumnName("payment_date").IsRequired();
        builder.Property(payment => payment.Note).HasColumnName("note").HasMaxLength(500);
        builder.Property(payment => payment.RecordedByUserId).HasColumnName("recorded_by_user_id").IsRequired();
        builder.Property(payment => payment.ConfirmationRequiredFromUserId)
            .HasColumnName("confirmation_required_from_user_id");
        builder.Property(payment => payment.Status)
            .HasColumnName("status")
            .HasConversion(
                status => status.ToString().ToUpperInvariant(),
                value => Enum.Parse<PaymentStatus>(value, ignoreCase: true))
            .HasMaxLength(20);
        builder.Property(payment => payment.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(payment => payment.RejectedAt).HasColumnName("rejected_at");
        builder.Property(payment => payment.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(payment => payment.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasOne(payment => payment.DebtShare)
            .WithMany()
            .HasForeignKey(payment => payment.DebtShareId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(payment => payment.DebtId).HasDatabaseName("ix_payments_debt_id");
        builder.HasIndex(payment => payment.DebtShareId).HasDatabaseName("ix_payments_debt_share_id");
        builder.HasIndex(payment => new { payment.ConfirmationRequiredFromUserId, payment.Status })
            .HasDatabaseName("ix_payments_confirmation_user_status");
    }
}
