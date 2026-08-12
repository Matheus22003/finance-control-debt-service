using FinanceControl.DebtService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceControl.DebtService.Persistence.Configurations;

internal sealed class FriendshipConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> builder)
    {
        builder.ToTable("friendships");
        builder.HasKey(friendship => friendship.Id);
        builder.Property(friendship => friendship.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(friendship => friendship.RequesterUserId).HasColumnName("requester_user_id");
        builder.Property(friendship => friendship.RequesterDisplayName)
            .HasColumnName("requester_display_name").HasMaxLength(120).IsRequired();
        builder.Property(friendship => friendship.RequesterEmail)
            .HasColumnName("requester_email").HasMaxLength(254).IsRequired();
        builder.Property(friendship => friendship.AddresseeUserId).HasColumnName("addressee_user_id");
        builder.Property(friendship => friendship.AddresseeDisplayName)
            .HasColumnName("addressee_display_name").HasMaxLength(120).IsRequired();
        builder.Property(friendship => friendship.AddresseeEmail)
            .HasColumnName("addressee_email").HasMaxLength(254).IsRequired();
        builder.Property(friendship => friendship.PairKey)
            .HasColumnName("pair_key").HasMaxLength(65).IsRequired();
        builder.Property(friendship => friendship.Status)
            .HasColumnName("status")
            .HasConversion(
                status => status.ToString().ToUpperInvariant(),
                value => Enum.Parse<FriendshipStatus>(value, ignoreCase: true))
            .HasMaxLength(20);
        builder.Property(friendship => friendship.CreatedAt).HasColumnName("created_at");
        builder.Property(friendship => friendship.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(friendship => friendship.PairKey)
            .HasDatabaseName("ux_friendships_pair_key").IsUnique();
        builder.HasIndex(friendship => new { friendship.AddresseeUserId, friendship.Status })
            .HasDatabaseName("ix_friendships_addressee_status");
        builder.HasIndex(friendship => new { friendship.RequesterUserId, friendship.Status })
            .HasDatabaseName("ix_friendships_requester_status");
    }
}
