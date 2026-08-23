using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Application.Users.GetMe;

public class GetMeQueryHandler(IWellSenseDbContext db) : IRequestHandler<GetMeQuery, GetMeResult>
{
    public async Task<GetMeResult> Handle(GetMeQuery request, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.CurrentUserId && !u.IsDeleted, ct)
            ?? throw AuthDomainException.AccountNotFound();

        return new GetMeResult(
            user.Id, user.Email, user.EmailVerified, user.Role.ToString(), user.Status.ToString(), user.CreatedAt);
    }
}
