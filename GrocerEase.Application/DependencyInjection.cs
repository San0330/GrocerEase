using FluentValidation;
using GrocerEase.Application.Authentication;
using GrocerEase.Application.Authentication.Validators;
using Microsoft.Extensions.DependencyInjection;

namespace GrocerEase.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthenticationService, AuthenticationService>();

        services.AddValidatorsFromAssemblyContaining<RegisterCommandValidator>();

        return services;
    }
}
