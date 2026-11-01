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

    public AuthenticationController(
        ICommandHandler<RegisterCommand, AuthenticationResult> register,
        ICommandHandler<LoginCommand, AuthenticationResult> login)
    {
        _register = register;
        _login = login;
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
        return StatusCode(StatusCodes.Status201Created, new AuthenticationResponse(
            result.Id,
            result.FirstName,
            result.LastName,
            result.Email,
            result.Token));
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

        return Ok(new AuthenticationResponse(
            result.Id,
            result.FirstName,
            result.LastName,
            result.Email,
            result.Token));
    }
}
