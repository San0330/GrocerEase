using GrocerEase.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace GrocerEase.Api;

public static class HealthEndpoints
{
    public const string Live = "/health/live";
    public const string Ready = "/health/ready";

    /// <summary>
    /// <c>live</c> says the process is up and answering, and runs no checks, so an orchestrator restarts the container
    /// only when it is truly stuck. <c>ready</c> also needs the database, so traffic is held back while it is unreachable
    /// without restarting an app that is fine. Both answer a bare status word: nothing about the system is revealed.
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(Live, new HealthCheckOptions { Predicate = _ => false });
        endpoints.MapHealthChecks(Ready, new HealthCheckOptions { Predicate = check => check.Tags.Contains(HealthCheckTags.Ready) });

        return endpoints;
    }
}
