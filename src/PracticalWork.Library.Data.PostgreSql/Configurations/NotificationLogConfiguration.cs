using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PracticalWork.Library.Data.PostgreSql.Entities;

namespace PracticalWork.Library.Data.PostgreSql.Configurations;

internal sealed class NotificationLogConfiguration : EntityConfigurationBase<NotificationLogEntity>
{
    public override void Configure(EntityTypeBuilder<NotificationLogEntity> builder)
    {
        base.Configure(builder);

        builder.Property(p => p.NotificationType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.RecipientEmail)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Subject)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.IsSent)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.ErrorMessage)
            .HasMaxLength(2000);

        builder
            .HasIndex(n => n.BorrowId);
        
        builder
            .HasIndex(n => n.NotificationType);
        
        builder
            .HasIndex(n => n.CreatedAt);
    }
}