using FluentValidation;
using GrocerEase.Application;
using GrocerEase.Application.Common.Handlers;
using GrocerEase.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NetArchTest.Rules;

namespace GrocerEase.UnitTests.Architecture;

/// <summary>The shapes this codebase has settled on, so the next feature copies them instead of inventing a variant.</summary>
public class ConventionTests
{
    [Fact]
    public void Controllers_AreControllersInTheControllersNamespace()
    {
        var result = Types.InAssembly(Layers.ApiAssembly)
            .That().HaveNameEndingWith("Controller")
            .Should().ResideInNamespace("GrocerEase.Api.Controllers")
            .And().Inherit(typeof(ControllerBase))
            .GetResult();

        AssertPasses(result);
    }

    [Fact]
    public void Controllers_TalkToTheApplicationThroughHandlers_NotToDataOrEntities()
    {
        var result = Types.InAssembly(Layers.ApiAssembly)
            .That().ResideInNamespace("GrocerEase.Api.Controllers")
            .Should().NotHaveDependencyOnAny(
                "GrocerEase.Domain.Entities",
                "GrocerEase.Application.Common.Interfaces.Persistence",
                Layers.Infrastructure)
            .GetResult();

        AssertPasses(result);
    }

    [Fact]
    public void Entities_ChangeOnlyThroughTheirOwnMethods()
    {
        var withPublicSetter = Layers.DomainAssembly.GetTypes()
            .Where(type => type.Namespace == "GrocerEase.Domain.Entities")
            .SelectMany(type => type.GetProperties().Select(property => (type, property)))
            .Where(pair => pair.property.SetMethod is { IsPublic: true })
            .Select(pair => $"{pair.type.Name}.{pair.property.Name}")
            .ToArray();

        Assert.True(withPublicSetter.Length == 0, "Public setters on entities: " + string.Join(", ", withPublicSetter));
    }

    [Fact]
    public void AppExceptions_LiveWhereTheyAreMeantToAndSayWhatTheyAre()
    {
        string[] homes = ["GrocerEase.Domain.Exceptions", "GrocerEase.Application.Common.Exceptions"];

        var misplaced = new[] { Layers.DomainAssembly, Layers.ApplicationAssembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IAppException).IsAssignableFrom(type))
            .Where(type => !homes.Contains(type.Namespace) || !type.Name.EndsWith("Exception") || !typeof(Exception).IsAssignableFrom(type))
            .Select(type => type.FullName)
            .ToArray();

        Assert.True(misplaced.Length == 0, "Not in an exceptions namespace, or not an Exception: " + string.Join(", ", misplaced));
    }

    [Fact]
    public void CommandHandlers_AreSealedAndNamedForWhatTheyHandle()
    {
        var handlers = ConcreteHandlers();

        Assert.NotEmpty(handlers);
        Assert.All(handlers, handler =>
        {
            Assert.True(handler.IsSealed, $"{handler.Name} should be sealed.");
            Assert.EndsWith("CommandHandler", handler.Name.Split('`')[0]);
        });
    }

    [Fact]
    public void EveryCommandHandler_IsRegisteredWithItsValidatorInFront()
    {
        // A handler that is not registered through AddCommandHandler would run on unvalidated input.
        var services = new ServiceCollection();
        services.AddApplication();

        foreach (var handler in ConcreteHandlers().Where(type => !type.IsGenericTypeDefinition))
        {
            var contract = handler.GetInterfaces().Single(IsCommandHandlerContract);
            var arguments = contract.GetGenericArguments();

            var registration = services.Where(d => d.ServiceType == contract).ToArray();
            Assert.True(registration.Length == 1, $"{handler.Name} is not registered exactly once as {contract.Name}.");
            Assert.NotNull(registration[0].ImplementationFactory);

            Assert.Contains(services, d => d.ServiceType == typeof(IValidator<>).MakeGenericType(arguments[0]));
        }
    }

    // The validating decorator is itself a handler, and is the one handler that is allowed to wrap others.
    private static Type[] ConcreteHandlers() => Layers.ApplicationAssembly.GetTypes()
        .Where(type => type is { IsClass: true, IsAbstract: false } && type.GetInterfaces().Any(IsCommandHandlerContract))
        .ToArray();

    private static bool IsCommandHandlerContract(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICommandHandler<,>);

    private static void AssertPasses(TestResult result) =>
        Assert.True(result.IsSuccessful, "Breaking a convention: " + string.Join(", ", result.FailingTypeNames ?? []));
}
