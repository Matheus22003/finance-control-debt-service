using FinanceControl.DebtService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceControl.DebtService.Persistence.Configurations;

internal sealed class DebtGroupMemberConfiguration : IEntityTypeConfiguration<DebtGroupMember>
{
    public void Configure(EntityTypeBuilder<DebtGroupMember> builder)
    {
        builder.ToTable("debt_group_members");
        builder.HasKey(member => member.Id);
        builder.Property(member => member.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(member => member.DebtGroupId).HasColumnName("debt_group_id");
        builder.Property(member => member.UserId).HasColumnName("user_id");
        builder.Property(member => member.DisplayName)
            .HasColumnName("display_name").HasMaxLength(120).IsRequired();
        builder.Property(member => member.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
        builder.Property(member => member.Role)
            .HasColumnName("role")
            .HasConversion(
                role => role.ToString().ToUpperInvariant(),
                value => Enum.Parse<GroupRole>(value, ignoreCase: true))
            .HasMaxLength(20);
        builder.Property(member => member.JoinedAt).HasColumnName("joined_at");
        builder.HasIndex(member => new { member.DebtGroupId, member.UserId })
            .HasDatabaseName("ux_debt_group_members_group_user").IsUnique();
        builder.HasIndex(member => member.UserId).HasDatabaseName("ix_debt_group_members_user_id");
    }
}
