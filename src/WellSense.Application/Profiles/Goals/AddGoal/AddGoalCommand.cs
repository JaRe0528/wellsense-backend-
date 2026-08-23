using MediatR;

namespace WellSense.Application.Profiles.Goals.AddGoal;

public record AddGoalCommand(Guid CurrentUserId, string Type, decimal TargetValue) : IRequest<Guid>;
