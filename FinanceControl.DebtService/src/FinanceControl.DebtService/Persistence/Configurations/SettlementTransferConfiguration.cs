using FinanceControl.DebtService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceControl.DebtService.Persistence.Configurations;

internal sealed class SettlementTransferConfiguration : IEntityTypeConfiguration<SettlementTransfer>
{
    public void Configure(EntityTypeBuilder<SettlementTransfer> builder)
    {
        builder.ToTable("settlement_transfers", tableBuilder =>
            tableBuilder.HasCheckConstraint("ck_settlement_transfers_amount_positive", "amount > 0"));
        builder.HasKey(transfer => transfer.Id);
        builder.Property(transfer => transfer.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(transfer => transfer.SettlementPlanId).HasColumnName("settlement_plan_id").IsRequired();
        builder.Property(transfer => transfer.FromIdentityId).HasColumnName("from_identity_id").IsRequired();
        builder.Property(transfer => transfer.FromUserId).HasColumnName("from_user_id");
        builder.Property(transfer => transfer.FromPersonId).HasColumnName("from_person_id").IsRequired();
        builder.Property(transfer => transfer.FromPersonName)
            .HasColumnName("from_person_name")
            .HasMaxLength(120)
            .IsRequired();
        builder.Property(transfer => transfer.ToIdentityId).HasColumnName("to_identity_id").IsRequired();
        builder.Property(transfer => transfer.ToUserId).HasColumnName("to_user_id");
        builder.Property(transfer => transfer.ToPersonId).HasColumnName("to_person_id").IsRequired();
        builder.Property(transfer => transfer.ToPersonName)
            .HasColumnName("to_person_name")
            .HasMaxLength(120)
            .IsRequired();
        builder.Property(transfer => transfer.Amount).HasColumnName("amount").HasPrecision(19, 2).IsRequired();
        builder.Property(transfer => transfer.PaymentDate).HasColumnName("payment_date");
        builder.Property(transfer => transfer.Note).HasColumnName("note").HasMaxLength(500);
        builder.Property(transfer => transfer.RecordedByUserId).HasColumnName("recorded_by_user_id");
        builder.Property(transfer => transfer.Status)
            .HasColumnName("status")
            .HasConversion(
                status => status.ToString().ToUpperInvariant(),
                value => Enum.Parse<SettlementTransferStatus>(value, ignoreCase: true))
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(transfer => transfer.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(transfer => transfer.RejectedAt).HasColumnName("rejected_at");
        builder.Property(transfer => transfer.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(transfer => transfer.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(transfer => new { transfer.ToUserId, transfer.Status })
            .HasDatabaseName("ix_settlement_transfers_to_user_status");
        builder.HasIndex(transfer => transfer.FromUserId)
            .HasDatabaseName("ix_settlement_transfers_from_user_id");
    }
}
