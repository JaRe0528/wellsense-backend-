using MediatR;

namespace WellSense.Application.Users.GetMe;

public record GetMeQuery(Guid CurrentUserId) : IRequest<GetMeResult>;

public record GetMeResult(
    Guid Id,
    string Email,
    bool EmailVerified,
    string Role,
    string Status,
    DateTimeOffset CreatedAt);
