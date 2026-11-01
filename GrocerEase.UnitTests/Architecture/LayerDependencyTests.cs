using NetArchTest.Rules;

namespace GrocerEase.UnitTests.Architecture;

/// <summary>
/// The dependency rule: code may depend inward, never outward. Domain is the centre; Application uses it; Infrastructure
/// and Api sit on the outside and plug into Application's interfaces. These tests read the compiled assemblies,
/// including method bodies, so a `using` that slips in fails the build of the next test run, not a code review.
/// </summary>
public class LayerDependencyTests
{
    [Fact]
    public void Domain_DependsOnNoOtherLayer()
    {
        var result = Types.InAssembly(Layers.DomainAssembly)
            .Should().NotHaveDependencyOnAny(Layers.Application, Layers.Infrastructure, Layers.Contracts, Layers.Api)
            .GetResult();

        AssertPasses(result);
    }

    [Fact]
    public void Domain_KnowsNoWebOrDatabaseFramework()
    {
        // Also keeps HTTP out: a status code or a controller type in Domain would show up as AspNetCore.
        var result = Types.InAssembly(Layers.DomainAssembly)
            .Should().NotHaveDependencyOnAny([.. Layers.WebAndPersistence, "FluentValidation"])
            .GetResult();

        AssertPasses(result);
    }

    [Fact]
    public void Application_DependsOnlyOnDomain()
    {
        var result = Types.InAssembly(Layers.ApplicationAssembly)
            .Should().NotHaveDependencyOnAny(Layers.Infrastructure, Layers.Contracts, Layers.Api)
            .GetResult();

        AssertPasses(result);
    }

    [Fact]
    public void Application_KnowsNoWebOrDatabaseFramework()
    {
        var result = Types.InAssembly(Layers.ApplicationAssembly)
            .Should().NotHaveDependencyOnAny(Layers.WebAndPersistence)
            .GetResult();

        AssertPasses(result);
    }

    [Fact]
    public void Contracts_IsPlainShapesWithNoDependencyOnTheRestOfTheSolution()
    {
        var result = Types.InAssembly(Layers.ContractsAssembly)
            .Should().NotHaveDependencyOnAny(Layers.Domain, Layers.Application, Layers.Infrastructure, Layers.Api)
            .GetResult();

        AssertPasses(result);
    }

    [Fact]
    public void Infrastructure_DoesNotKnowTheApiOrItsShapes()
    {
        var result = Types.InAssembly(Layers.InfrastructureAssembly)
            .Should().NotHaveDependencyOnAny(Layers.Api, Layers.Contracts)
            .GetResult();

        AssertPasses(result);
    }

    [Fact]
    public void Infrastructure_DoesNotOwnTheHttpPipeline()
    {
        // Validating a bearer token is an HTTP concern and lives in Api (JwtAuthentication).
        var result = Types.InAssembly(Layers.InfrastructureAssembly)
            .Should().NotHaveDependencyOn("Microsoft.AspNetCore.Authentication")
            .GetResult();

        AssertPasses(result);
    }

    [Fact]
    public void Api_DoesNotUseTheDatabaseDirectly()
    {
        // Infrastructure translates database failures (UnitOfWork), so the Api never sees EF Core or Npgsql types.
        var result = Types.InAssembly(Layers.ApiAssembly)
            .Should().NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql")
            .GetResult();

        AssertPasses(result);
    }

    [Fact]
    public void Api_ReachesIntoInfrastructureOnlyWhereItComposesTheApp()
    {
        // Api is the composition root, so some code must name Infrastructure. Keeping that list short and explicit means
        // a controller or middleware that starts depending on a repository or a setting shows up here as a new entry.
        // (A `const` from Infrastructure is copied in by the compiler, so it is not a dependency and is not listed.)
        string[] allowed =
        [
            "Program",
            "GrocerEase.Api.Authentication.JwtAuthentication",
        ];

        var offenders = Types.InAssembly(Layers.ApiAssembly)
            .That().HaveDependencyOn(Layers.Infrastructure)
            .GetTypes()
            .Select(type => TopLevelName(type.FullName!))
            .Distinct()
            .Order()
            .ToArray();

        Assert.Equal(allowed.Order(), offenders);
    }

    // Closures and state machines are nested types, and belong to the class that declares them.
    private static string TopLevelName(string fullName) => fullName.Split('+')[0];

    private static void AssertPasses(TestResult result) =>
        Assert.True(result.IsSuccessful, "Breaking the dependency rule: " + string.Join(", ", result.FailingTypeNames ?? []));
}
