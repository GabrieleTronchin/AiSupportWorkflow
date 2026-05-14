namespace AiSupportWorkflow.Infrastructure.Persistence.Configurations;

using AiSupportWorkflow.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class LlmCallRecordConfiguration : IEntityTypeConfiguration<LlmCallRecord>
{
    public void Configure(EntityTypeBuilder<LlmCallRecord> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.AgentId);
        builder.HasIndex(e => e.Timestamp);
    }
}
