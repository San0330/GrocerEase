using System.Text;
using GrocerEase.Api.Authentication;
using GrocerEase.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GrocerEase.UnitTests.Api;

public class JwtAuthenticationTests
{
    private const string Secret = "unit-test-secret-key-at-least-32-chars";

    [Fact]
    public void AddJwtAuthentication_ValidatesEverythingAgainstTheConfiguredSettings()
    {
        var options = BearerOptions();

        var parameters = options.TokenValidationParameters;
        Assert.True(parameters.ValidateIssuer);
        Assert.True(parameters.ValidateAudience);
        Assert.True(parameters.ValidateLifetime);
        Assert.True(parameters.ValidateIssuerSigningKey);
        Assert.Equal("test-issuer", parameters.ValidIssuer);
        Assert.Equal("test-audience", parameters.ValidAudience);

        var key = Assert.IsType<SymmetricSecurityKey>(parameters.IssuerSigningKey);
        Assert.Equal(Encoding.UTF8.GetBytes(Secret), key.Key);
    }

    [Fact]
    public void AddJwtAuthentication_AnswersAChallengeInTheApiErrorShape()
    {
        Assert.NotNull(BearerOptions().Events.OnChallenge);
    }

    private static JwtBearerOptions BearerOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = Secret,
                ["JwtSettings:Issuer"] = "test-issuer",
                ["JwtSettings:Audience"] = "test-audience",
                ["JwtSettings:ExpiryMinutes"] = "60",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<JwtSettings>().Bind(configuration.GetSection(JwtSettings.SectionName));
        services.AddJwtAuthentication();

        return services.BuildServiceProvider()
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
    }
}
