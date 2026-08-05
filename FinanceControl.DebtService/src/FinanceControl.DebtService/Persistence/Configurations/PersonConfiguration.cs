using FinanceControl.DebtService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceControl.DebtService.Persistence.Configurations;

internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("people");
        builder.HasKey(person => person.Id);
        builder.Property(person => person.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(person => person.OwnerUserId).HasColumnName("owner_user_id").IsRequired();
        builder.Property(person => person.LinkedUserId).HasColumnName("linked_user_id");
        builder.Property(person => person.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(person => person.Email).HasColumnName("email").HasMaxLength(254);
        builder.Property(person => person.IsCurrentUser).HasColumnName("is_current_user").IsRequired();
        builder.Property(person => person.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(person => person.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(person => new { person.OwnerUserId, person.Email })
            .HasDatabaseName("ix_people_owner_email");
        builder.HasIndex(person => new { person.OwnerUserId, person.IsCurrentUser })
            .HasDatabaseName("ux_people_current_user")
            .IsUnique()
            .HasFilter("is_current_user = TRUE");
        builder.HasIndex(person => person.LinkedUserId)
            .HasDatabaseName("ix_people_linked_user_id");
    }
}
