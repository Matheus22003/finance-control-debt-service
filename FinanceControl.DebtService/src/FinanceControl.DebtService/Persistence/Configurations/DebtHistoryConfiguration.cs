using FinanceControl.DebtService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceControl.DebtService.Persistence.Configurations;

internal sealed class DebtHistoryConfiguration : IEntityTypeConfiguration<DebtHistory>
{
    public void Configure(EntityTypeBuilder<DebtHistory> builder)
    {
        builder.ToTable("debt_history");
        builder.HasKey(history => history.Id);
        builder.Property(history => history.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(history => history.DebtId).HasColumnName("debt_id").IsRequired();
        builder.Property(history => history.Type)
            .HasColumnName("type")
            .HasConversion(
                type => type.ToString().ToUpperInvariant(),
                value => Enum.Parse<DebtHistoryType>(value, ignoreCase: true))
            .HasMaxLength(30);
        builder.Property(history => history.Description).HasColumnName("description").HasMaxLength(500).IsRequired();
        builder.Property(history => history.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.HasIndex(history => new { history.DebtId, history.OccurredAt })
            .HasDatabaseName("ix_debt_history_debt_occurred_at");
    }
}
