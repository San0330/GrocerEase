using System.Text;
using GrocerEase.Api.Errors;
using GrocerEase.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GrocerEase.Api.Authentication;

/// <summary>
/// The bearer scheme that protects endpoints. Validating a token is an HTTP concern, so the scheme is set up here in
/// one place; Infrastructure only issues tokens. The signing key and lifetimes come from <see cref="JwtSettings"/>,
/// which <c>AddInfrastructure</c> binds and validates.
/// </summary>
public static class JwtAuthentication
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Configured through IOptions<JwtSettings> so the key is read after the user-secrets and environment
        // sources are loaded, and after the settings have been validated at startup.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((options, jwtOptions) =>
            {
                var jwtSettings = jwtOptions.Value;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                };

                // The JwtBearer handler writes the challenge for a missing or invalid token itself, outside the
                // error middleware, so it needs its own hook to answer in the API's error shape.
                options.Events = new JwtBearerEvents { OnChallenge = UnauthorizedResponse.WriteAsync };
            });

        return services;
    }
}
