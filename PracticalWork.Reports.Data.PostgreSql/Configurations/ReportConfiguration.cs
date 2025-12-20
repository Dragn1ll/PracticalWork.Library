using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PracticalWork.Reports.Data.PostgreSql.Entities;

namespace PracticalWork.Reports.Data.PostgreSql.Configurations;

public class ReportConfiguration : EntityConfigurationBase<ReportEntity>
{
    public override void Configure(EntityTypeBuilder<ReportEntity> builder)
    {
        base.Configure(builder);
        
        builder.Property(p => p.Name)
            .HasMaxLength(255)
            .IsRequired();
        
        builder.Property(p => p.FilePath)
            .HasMaxLength(500)
            .IsRequired();
        
        builder.Property(b => b.PeriodFrom)
            .IsRequired();
        
        builder.Property(b => b.PeriodTo)
            .IsRequired();
        
        builder.Property(b => b.Status)
            .IsRequired();
    }
}