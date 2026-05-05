namespace AiSupportWorkflow.Infrastructure.Persistence.Configurations;

using AiSupportWorkflow.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class IssueEntityConfiguration : IEntityTypeConfiguration<IssueEntity>
{
    public void Configure(EntityTypeBuilder<IssueEntity> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CurrentStage).HasConversion<string>();
        builder.HasIndex(e => e.CurrentStage);
    }
}
