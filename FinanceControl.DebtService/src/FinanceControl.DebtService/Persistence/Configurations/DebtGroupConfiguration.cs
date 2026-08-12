using FinanceControl.DebtService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceControl.DebtService.Persistence.Configurations;

internal sealed class DebtGroupConfiguration : IEntityTypeConfiguration<DebtGroup>
{
    public void Configure(EntityTypeBuilder<DebtGroup> builder)
    {
        builder.ToTable("debt_groups");
        builder.HasKey(group => group.Id);
        builder.Property(group => group.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(group => group.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(group => group.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(group => group.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(group => group.CreatedAt).HasColumnName("created_at");
        builder.Property(group => group.UpdatedAt).HasColumnName("updated_at");
        builder.HasMany(group => group.Members)
            .WithOne()
            .HasForeignKey(member => member.DebtGroupId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(group => group.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(group => group.CreatedByUserId).HasDatabaseName("ix_debt_groups_created_by");
    }
}
