using FluentValidation;
using GrocerEase.Application.Authentication;
using GrocerEase.Application.Authentication.Commands;
using GrocerEase.Application.Authentication.Handlers;
using GrocerEase.Application.Authentication.Validators;
using GrocerEase.Application.Common.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace GrocerEase.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterCommandValidator>();

        services.AddCommandHandler<RegisterCommand, AuthenticationResult, RegisterCommandHandler>();
        services.AddCommandHandler<LoginCommand, AuthenticationResult, LoginCommandHandler>();
        services.AddCommandHandler<RefreshTokenCommand, AuthenticationResult, RefreshTokenCommandHandler>();
        services.AddCommandHandler<LogoutCommand, Unit, LogoutCommandHandler>();

        return services;
    }

    /// <summary>
    /// Registers a handler behind <see cref="ICommandHandler{TCommand,TResult}"/> with its command's validator in front.
    /// A new use case adds one line here and gets validation without writing it.
    /// </summary>
    private static void AddCommandHandler<TCommand, TResult, THandler>(this IServiceCollection services)
        where THandler : class, ICommandHandler<TCommand, TResult>
    {
        services.AddScoped<THandler>();
        services.AddScoped<ICommandHandler<TCommand, TResult>>(provider =>
            new ValidatingCommandHandler<TCommand, TResult>(
                provider.GetRequiredService<THandler>(),
                provider.GetRequiredService<IValidator<TCommand>>()));
    }
}
