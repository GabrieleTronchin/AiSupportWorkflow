namespace AiSupportWorkflow.Infrastructure.Persistence.Configurations;

using AiSupportWorkflow.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.ReceivedAt);
        builder.HasIndex(e => e.ProcessedAt);
    }
}
