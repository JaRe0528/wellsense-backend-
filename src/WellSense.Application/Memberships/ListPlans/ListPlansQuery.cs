using MediatR;

namespace WellSense.Application.Memberships.ListPlans;

public record ListPlansQuery : IRequest<IReadOnlyList<PlanResult>>;

public record PlanLimitsResult(int? MaxDevices, int? HistoryDays);

public record PlanResult(
    Guid Id, string Code, string Name, int PriceCents, string Currency,
    IReadOnlyList<string> Features, PlanLimitsResult Limits);
