using System.Threading.RateLimiting;
using GrocerEase.Api.Errors;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace GrocerEase.Api.RateLimiting;

/// <summary>
/// Caps register and login attempts per client address. Each attempt costs a BCrypt hash, so without a cap
/// the endpoints invite both password guessing and CPU exhaustion.
/// </summary>
public static partial class AuthRateLimiting
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

        // The caller's address and the endpoint, nothing from the body: only /auth/* is limited, so the path is one of a
        // few known values, and the address is what a repeated attempt can be traced to.
        context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(AuthRateLimiting).FullName!)
            .RateLimitRejected(
                context.HttpContext.Request.Path.Value ?? string.Empty,
                context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? UnknownClient);

        await ApiProblem.WriteAsync(
            context.HttpContext,
            ApiProblem.Error(
                context.HttpContext,
                ApiErrorCodes.RateLimited,
                StatusCodes.Status429TooManyRequests,
                "Too Many Requests",
                "Too many requests. Try again later."),
            cancellationToken);
    }

    [LoggerMessage(
        EventId = 1009,
        Level = LogLevel.Warning,
        Message = "Rate limit exceeded on {Path} from {ClientAddress}")]
    private static partial void RateLimitRejected(this ILogger logger, string path, string clientAddress);
}
