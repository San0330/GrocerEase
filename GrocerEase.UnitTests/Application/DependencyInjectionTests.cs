using FluentValidation;
using GrocerEase.Application;
using GrocerEase.Application.Authentication;
using GrocerEase.Application.Authentication.Commands;
using GrocerEase.Application.Authentication.Handlers;
using GrocerEase.Application.Authentication.Validators;
using GrocerEase.Application.Common.Handlers;
using GrocerEase.Application.Common.Interfaces.Authentication;
using GrocerEase.Application.Common.Interfaces.Persistence;
using GrocerEase.Application.Common.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace GrocerEase.UnitTests.Application;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_RegistersTheValidators()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        var register = Assert.Single(services, d => d.ServiceType == typeof(IValidator<RegisterCommand>));
        Assert.Equal(typeof(RegisterCommandValidator), register.ImplementationType);

        var login = Assert.Single(services, d => d.ServiceType == typeof(IValidator<LoginCommand>));
        Assert.Equal(typeof(LoginCommandValidator), login.ImplementationType);

        var refresh = Assert.Single(services, d => d.ServiceType == typeof(IValidator<RefreshTokenCommand>));
        Assert.Equal(typeof(RefreshTokenCommandValidator), refresh.ImplementationType);

        var logout = Assert.Single(services, d => d.ServiceType == typeof(IValidator<LogoutCommand>));
        Assert.Equal(typeof(LogoutCommandValidator), logout.ImplementationType);
    }

    [Fact]
    public void AddApplication_PutsTheValidatorInFrontOfEveryHandler()
    {
        using var provider = BuildProvider();

        Assert.IsType<ValidatingCommandHandler<RegisterCommand, AuthenticationResult>>(
            provider.GetRequiredService<ICommandHandler<RegisterCommand, AuthenticationResult>>());
        Assert.IsType<ValidatingCommandHandler<LoginCommand, AuthenticationResult>>(
            provider.GetRequiredService<ICommandHandler<LoginCommand, AuthenticationResult>>());
        Assert.IsType<ValidatingCommandHandler<RefreshTokenCommand, AuthenticationResult>>(
            provider.GetRequiredService<ICommandHandler<RefreshTokenCommand, AuthenticationResult>>());
        Assert.IsType<ValidatingCommandHandler<LogoutCommand, Unit>>(
            provider.GetRequiredService<ICommandHandler<LogoutCommand, Unit>>());
    }

    [Fact]
    public async Task AddApplication_RejectsAnInvalidCommandBeforeTheHandlerTouchesAnything()
    {
        var users = new Mock<IUserRepository>();
        using var provider = BuildProvider(users);
        var handler = provider.GetRequiredService<ICommandHandler<RegisterCommand, AuthenticationResult>>();

        await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(new RegisterCommand("Ada", "Lovelace", "not-an-email", "Passw0rd!")));

        users.VerifyNoOtherCalls();
    }

    [Fact]
    public void AddApplication_ScopesTheHandlersToTheRequest()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        Assert.All(
            services.Where(d => d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)),
            d => Assert.Equal(ServiceLifetime.Scoped, d.Lifetime));
    }

    private static ServiceProvider BuildProvider(Mock<IUserRepository>? users = null)
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddSingleton(Mock.Of<IJwtTokenGenerator>());
        services.AddSingleton(Mock.Of<IPasswordHasher>());
        services.AddSingleton((users ?? new Mock<IUserRepository>()).Object);
        services.AddSingleton(Mock.Of<IUnitOfWork>());
        services.AddSingleton(Mock.Of<IDateTimeProvider>());
        services.AddSingleton(Mock.Of<IRefreshTokenGenerator>());
        services.AddSingleton(Mock.Of<IRefreshTokenRepository>());
        return services.BuildServiceProvider();
    }
}
