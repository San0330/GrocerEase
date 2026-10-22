using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GrocerEase.Api.Controllers;
using GrocerEase.Contracts.Profile;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GrocerEase.UnitTests.Api;

public class ProfileControllerTests
{
    private static readonly Guid UserId = Guid.Parse("018f6b2e-7c3a-7a1b-8c2d-4e5f60718293");

    [Fact]
    public void Get_WithJwtSubjectAndEmail_ReturnsTheProfile()
    {
        var controller = ControllerWith(
            new Claim(JwtRegisteredClaimNames.Sub, UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, "ada@example.com"));

        var result = controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var profile = Assert.IsType<ProfileResponse>(ok.Value);
        Assert.Equal(UserId, profile.Id);
        Assert.Equal("ada@example.com", profile.Email);
    }

    [Fact]
    public void Get_FallsBackToNameIdentifierAndEmailClaimTypes()
    {
        var controller = ControllerWith(
            new Claim(ClaimTypes.NameIdentifier, UserId.ToString()),
            new Claim(ClaimTypes.Email, "ada@example.com"));

        var result = controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var profile = Assert.IsType<ProfileResponse>(ok.Value);
        Assert.Equal(UserId, profile.Id);
        Assert.Equal("ada@example.com", profile.Email);
    }

    [Fact]
    public void Get_PrefersTheJwtSubjectWhenBothIdentifiersArePresent()
    {
        var jwtId = Guid.Parse("018f6b2e-7c3a-7a1b-8c2d-4e5f60718293");
        var otherId = Guid.Parse("018f6b2e-7c3a-7a1b-8c2d-4e5f60718294");
        var controller = ControllerWith(
            new Claim(JwtRegisteredClaimNames.Sub, jwtId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, otherId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, "jwt@example.com"),
            new Claim(ClaimTypes.Email, "other@example.com"));

        var result = controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var profile = Assert.IsType<ProfileResponse>(ok.Value);
        Assert.Equal(jwtId, profile.Id);
        Assert.Equal("jwt@example.com", profile.Email);
    }

    [Theory]
    [MemberData(nameof(IncompleteClaims))]
    public void Get_WithoutSubjectOrEmail_ReturnsUnauthorized(Claim[] claims)
    {
        var controller = ControllerWith(claims);

        var result = controller.Get();

        Assert.IsType<UnauthorizedResult>(result);
    }

    public static TheoryData<Claim[]> IncompleteClaims() =>
    [
        [],
        [new Claim(JwtRegisteredClaimNames.Sub, UserId.ToString())],
        [new Claim(JwtRegisteredClaimNames.Email, "ada@example.com")],
    ];

    private static ProfileController ControllerWith(params Claim[] claims) =>
        new()
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Bearer")),
                },
            },
        };
}
