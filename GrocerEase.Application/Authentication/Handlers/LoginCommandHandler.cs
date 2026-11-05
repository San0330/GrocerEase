using GrocerEase.Application.Authentication.Commands;
using GrocerEase.Application.Common.Handlers;
using GrocerEase.Application.Common.Interfaces.Authentication;
using GrocerEase.Application.Common.Interfaces.Persistence;
using GrocerEase.Application.Common.Interfaces.Services;
using GrocerEase.Application.Common.Telemetry;
using GrocerEase.Domain.Entities;
using GrocerEase.Domain.Exceptions;
using GrocerEase.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace GrocerEase.Application.Authentication.Handlers;

public sealed class LoginCommandHandler : ICommandHandler<LoginCommand, AuthenticationResult>
{
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<LoginCommandHandler> _logger;
    private readonly GrocerEaseMetrics _metrics;

    public LoginCommandHandler(
        IJwtTokenGenerator jwtTokenGenerator,
        IPasswordHasher passwordHasher,
        IUserRepository userRepository,
        IRefreshTokenGenerator refreshTokenGenerator,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        ILogger<LoginCommandHandler> logger,
        GrocerEaseMetrics metrics)
    {
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordHasher = passwordHasher;
        _userRepository = userRepository;
        _refreshTokenGenerator = refreshTokenGenerator;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<AuthenticationResult> HandleAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetUserByEmailAsync(Email.Create(command.Email), cancellationToken);

        // Verify even when there is no user, so a wrong email is not answered faster than a wrong password.
        var passwordMatches = _passwordHasher.Verify(command.Password, user?.PasswordHash);

        if (user is null || !passwordMatches)
        {
            // The two reasons are for the operator. The caller gets the same answer for both.
            _logger.LoginFailed(
                user is null ? AuthenticationLog.Reasons.UnknownEmail : AuthenticationLog.Reasons.WrongPassword,
                EmailFingerprint.Of(command.Email));
            _metrics.AuthAttempt(GrocerEaseMetrics.Operations.Login, succeeded: false);
            throw new InvalidCredentialsException();
        }

        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.FirstName, user.LastName, user.Email.Value);

        // Each login starts its own family, so signing out of one device does not sign out the others.
        var refresh = _refreshTokenGenerator.Issue();
        await _refreshTokenRepository.AddAsync(
            RefreshToken.StartFamily(user.Id, refresh.Hash, _clock.UtcNow, refresh.ExpiresAt),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LoginSucceeded(user.Id);
        _metrics.AuthAttempt(GrocerEaseMetrics.Operations.Login, succeeded: true);

        return AuthenticationResult.From(user, token, refresh.Token);
    }
}
