using MediatR;

namespace WellSense.Application.Profiles.Goals.DeleteGoal;

public record DeleteGoalCommand(Guid CurrentUserId, Guid GoalId) : IRequest<Unit>;
