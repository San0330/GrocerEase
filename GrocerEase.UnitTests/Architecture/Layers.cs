using System.Reflection;
using GrocerEase.Application.Common.Handlers;
using GrocerEase.Contracts.Errors;
using GrocerEase.Domain.Entities;

namespace GrocerEase.UnitTests.Architecture;

/// <summary>The five production assemblies and their root namespaces, in one place for the architecture tests.</summary>
internal static class Layers
{
    public const string Domain = "GrocerEase.Domain";
    public const string Application = "GrocerEase.Application";
    public const string Infrastructure = "GrocerEase.Infrastructure";
    public const string Contracts = "GrocerEase.Contracts";
    public const string Api = "GrocerEase.Api";

    public static readonly Assembly DomainAssembly = typeof(User).Assembly;
    public static readonly Assembly ApplicationAssembly = typeof(ICommandHandler<,>).Assembly;
    public static readonly Assembly InfrastructureAssembly = typeof(GrocerEase.Infrastructure.HealthCheckTags).Assembly;
    public static readonly Assembly ContractsAssembly = typeof(ErrorResponse).Assembly;
    public static readonly Assembly ApiAssembly = typeof(GrocerEase.Api.HealthEndpoints).Assembly;

    /// <summary>Frameworks that tie code to HTTP or to a particular database.</summary>
    public static readonly string[] WebAndPersistence =
    [
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
    ];
}
