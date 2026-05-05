namespace AiSupportWorkflow.Infrastructure.Persistence.Configurations;

using AiSupportWorkflow.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class StateTransitionEventConfiguration : IEntityTypeConfiguration<StateTransitionEvent>
{
    public void Configure(EntityTypeBuilder<StateTransitionEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.IssueId);
        builder.HasIndex(e => e.Timestamp);
        builder.Property(e => e.PreviousStage).HasConversion<string>();
        builder.Property(e => e.NewStage).HasConversion<string>();
    }
}
