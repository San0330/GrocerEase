using FluentValidation;
using GrocerEase.Application;
using GrocerEase.Application.Authentication;
using GrocerEase.Application.Authentication.Commands;
using GrocerEase.Application.Authentication.Validators;
using Microsoft.Extensions.DependencyInjection;

namespace GrocerEase.UnitTests.Application;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_RegistersAuthenticationServiceAndValidators()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        var authentication = Assert.Single(services, d => d.ServiceType == typeof(IAuthenticationService));
        Assert.Equal(typeof(AuthenticationService), authentication.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, authentication.Lifetime);

        var register = Assert.Single(services, d => d.ServiceType == typeof(IValidator<RegisterCommand>));
        Assert.Equal(typeof(RegisterCommandValidator), register.ImplementationType);

        var login = Assert.Single(services, d => d.ServiceType == typeof(IValidator<LoginCommand>));
        Assert.Equal(typeof(LoginCommandValidator), login.ImplementationType);
    }
}
