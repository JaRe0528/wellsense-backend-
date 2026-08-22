using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace WellSense.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped(typeof(MediatR.IPipelineBehavior<,>), typeof(Common.Behaviors.ValidationBehavior<,>));
        return services;
    }
}
