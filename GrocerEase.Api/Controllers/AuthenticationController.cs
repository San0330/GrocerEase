using GrocerEase.Api.RateLimiting;
using GrocerEase.Application.Authentication;
using GrocerEase.Application.Authentication.Commands;
using GrocerEase.Application.Common.Handlers;
using GrocerEase.Contracts.Authentication;
using GrocerEase.Contracts.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GrocerEase.Api.Controllers;

[ApiController]
[Route("auth")]
[EnableRateLimiting(AuthRateLimiting.PolicyName)]
[Produces("application/json")]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status500InternalServerError, "application/problem+json")]
[ProducesResponseType<ValidationErrorResponse>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
public class AuthenticationController : ControllerBase
{
    private readonly ICommandHandler<RegisterCommand, AuthenticationResult> _register;
    private readonly ICommandHandler<LoginCommand, AuthenticationResult> _login;
    private readonly ICommandHandler<RefreshTokenCommand, AuthenticationResult> _refresh;

    public AuthenticationController(
        ICommandHandler<RegisterCommand, AuthenticationResult> register,
        ICommandHandler<LoginCommand, AuthenticationResult> login,
        ICommandHandler<RefreshTokenCommand, AuthenticationResult> refresh)
    {
        _register = register;
        _login = login;
        _refresh = refresh;
    }

    [HttpPost("register")]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict, "application/problem+json")]
    // A malformed body is a ValidationErrorResponse. A database constraint failure is an ErrorResponse: the OpenAPI
    // transformer publishes both shapes for this status.
    [ProducesResponseType<ValidationErrorResponse>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var command = new RegisterCommand(
            request.FirstName,
            request.LastName,
            request.Email,
            request.Password);

        var result = await _register.HandleAsync(command, cancellationToken);

        // 201 without a Location header: the user has no address of its own to point at yet.
        return StatusCode(StatusCodes.Status201Created, ToResponse(result));
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ValidationErrorResponse>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var command = new LoginCommand(
            request.Email,
            request.Password);

        var result = await _login.HandleAsync(command, cancellationToken);

        return Ok(ToResponse(result));
    }

    /// <summary>Trades a refresh token for a new access token and a new refresh token. The old refresh token stops working.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    // Two requests presenting the same token at once: one succeeds and the other is told to try again.
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ValidationErrorResponse>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await _refresh.HandleAsync(new RefreshTokenCommand(request.RefreshToken), cancellationToken);

        return Ok(ToResponse(result));
    }

    private static AuthenticationResponse ToResponse(AuthenticationResult result) => new AuthenticationResponse(
            result.Id,
            result.FirstName,
            result.LastName,
            result.Email,
            result.Token,
            result.RefreshToken);
}
