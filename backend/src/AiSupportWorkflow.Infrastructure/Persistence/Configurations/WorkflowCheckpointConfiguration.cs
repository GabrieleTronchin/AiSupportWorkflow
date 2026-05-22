namespace AiSupportWorkflow.Infrastructure.Persistence.Configurations;

using AiSupportWorkflow.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class WorkflowCheckpointConfiguration : IEntityTypeConfiguration<WorkflowCheckpoint>
{
    public void Configure(EntityTypeBuilder<WorkflowCheckpoint> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.IssueId);
        builder.HasIndex(e => e.IsActive);
    }
}
