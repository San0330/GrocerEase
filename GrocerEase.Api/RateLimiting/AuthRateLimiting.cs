using System.Threading.RateLimiting;
using GrocerEase.Api.Errors;
using GrocerEase.Api.MiddleWare;
using GrocerEase.Contracts.Errors;
using GrocerEase.Domain.Exceptions;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace GrocerEase.Api.RateLimiting;

/// <summary>
/// Caps register and login attempts per client address. Each attempt costs a BCrypt hash, so without a cap
/// the endpoints invite both password guessing and CPU exhaustion.
/// </summary>
public static class AuthRateLimiting
{
    public const string PolicyName = "auth";

    // Requests with no address (an in-memory test server) share one bucket.
    private const string UnknownClient = "unknown";

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthRateLimitSettings>()
            .Bind(configuration.GetSection(AuthRateLimitSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectionAsync;
        });

        // Added separately so the limits come from the validated options and not from a second read of configuration.
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<AuthRateLimitSettings>>((options, settings) =>
                options.AddPolicy(PolicyName, context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? UnknownClient,
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = settings.Value.PermitLimit,
                            Window = TimeSpan.FromSeconds(settings.Value.WindowSeconds),
                            QueueLimit = 0,
                        })));

        return services;
    }

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }

        var body = new ErrorResponse(
            ApiErrorCodes.RateLimited,
            StatusCodes.Status429TooManyRequests,
            "Too Many Requests",
            "Too many requests. Try again later.",
            RequestTrace.IdFor(context.HttpContext));

        response.StatusCode = StatusCodes.Status429TooManyRequests;
        await response.WriteAsJsonAsync(body, options: null, contentType: "application/problem+json", cancellationToken);
    }
}
