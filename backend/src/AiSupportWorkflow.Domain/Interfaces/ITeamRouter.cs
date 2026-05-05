namespace AiSupportWorkflow.Domain.Interfaces;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.ValueObjects;

public interface ITeamRouter
{
    Result<TeamAssignment> Route(IssueRecord issue, ClassificationResult classification);
}
