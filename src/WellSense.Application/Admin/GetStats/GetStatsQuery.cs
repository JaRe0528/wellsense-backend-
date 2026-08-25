using MediatR;

namespace WellSense.Application.Admin.GetStats;

public record GetStatsQuery : IRequest<AdminStatsResult>;

public record PlanDistributionItem(string PlanCode, int UserCount);

public record AdminStatsResult(int TotalUsers, int ActiveUsersLast7Days, IReadOnlyList<PlanDistributionItem> UsersByPlan);
