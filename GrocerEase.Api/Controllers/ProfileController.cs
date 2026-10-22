using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GrocerEase.Contracts.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrocerEase.Api.Controllers;

[ApiController]
[Route("profile")]
[Authorize]
[Produces("application/json")]
[ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
public class ProfileController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        var email = User.FindFirstValue(JwtRegisteredClaimNames.Email)
                    ?? User.FindFirstValue(ClaimTypes.Email);

        if (sub is null || email is null)
            return Unauthorized();

        return Ok(new ProfileResponse(Guid.Parse(sub), email));
    }
}
