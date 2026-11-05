using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace GrocerEase.Api.Cors;

/// <summary>
/// Lets the browser frontend call the API from its own origin. The list of origins comes from configuration so
/// each environment names its own; an origin that is not listed gets no CORS headers and the browser blocks it.
/// </summary>
public static class FrontendCors
{
    public const string PolicyName = "frontend";

    public static IServiceCollection AddFrontendCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CorsSettings>()
            .Bind(configuration.GetSection(CorsSettings.SectionName))
            .Validate(
                settings => settings.AllowedOrigins.All(IsPlainOrigin),
                "Cors:AllowedOrigins entries must be an origin such as http://localhost:3000: " +
                "http or https, a host, an optional port, and no path or trailing slash.")
            .ValidateOnStart();

        // Added separately so the policy is built from the validated options.
        services.AddOptions<CorsOptions>()
            .Configure<IOptions<CorsSettings>>((options, settings) =>
                options.AddPolicy(PolicyName, policy => policy
                    .WithOrigins(settings.Value.AllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    // Not readable by browser code unless exposed, and the frontend needs it to back off after a 429.
                    .WithExposedHeaders("Retry-After")));

        // Authentication is a bearer token in the Authorization header, not a cookie, so credentials stay off.
        services.AddCors();

        return services;
    }

    // A trailing slash or a path never matches the Origin header a browser sends, so the policy would silently
    // allow nothing. Failing at startup is better than a frontend that cannot connect for no visible reason.
    private static bool IsPlainOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && uri.GetLeftPart(UriPartial.Authority) == origin;
}
