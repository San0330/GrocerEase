using GrocerEase.Application.Authentication.Commands;
using GrocerEase.Application.Common.Handlers;
using GrocerEase.Application.Common.Interfaces.Authentication;
using GrocerEase.Application.Common.Interfaces.Persistence;
using GrocerEase.Application.Common.Interfaces.Services;
using GrocerEase.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace GrocerEase.Application.Authentication.Handlers;

public sealed class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand, AuthenticationResult>
{
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IJwtTokenGenerator jwtTokenGenerator,
        IRefreshTokenGenerator refreshTokenGenerator,
        IRefreshTokenRepository refreshTokenRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _jwtTokenGenerator = jwtTokenGenerator;
        _refreshTokenGenerator = refreshTokenGenerator;
        _refreshTokenRepository = refreshTokenRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<AuthenticationResult> HandleAsync(RefreshTokenCommand command, CancellationToken cancellationToken = default)
    {
        var hash = _refreshTokenGenerator.Hash(command.RefreshToken);
        var presented = await _refreshTokenRepository.GetByHashAsync(hash, cancellationToken);
        if (presented is null)
        {
            _logger.RefreshRejected(AuthenticationLog.Reasons.UnknownToken);
            throw new InvalidRefreshTokenException();
        }

        var now = _clock.UtcNow;

        if (presented.IsRevoked)
        {
            // Every use revokes the token it replaces, so a revoked token coming back means a copy exists somewhere.
            // The owner and the holder of the copy cannot be told apart, so the whole family goes.
            var revoked = await RevokeFamilyAsync(presented.FamilyId, now);
            _logger.RefreshTokenReuseDetected(presented.UserId, presented.FamilyId, revoked);
            throw new InvalidRefreshTokenException();
        }

        if (presented.IsExpired(now))
        {
            _logger.RefreshRejected(AuthenticationLog.Reasons.ExpiredToken);
            throw new InvalidRefreshTokenException();
        }

        var user = await _userRepository.GetByIdAsync(presented.UserId, cancellationToken);
        if (user is null)
        {
            _logger.RefreshRejected(AuthenticationLog.Reasons.UserMissing);
            throw new InvalidRefreshTokenException();
        }

        var refresh = _refreshTokenGenerator.Issue();
        var successor = presented.Rotate(refresh.Hash, now, refresh.ExpiresAt);
        await _refreshTokenRepository.AddAsync(successor, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.FirstName, user.LastName, user.Email.Value);

        _logger.TokenRefreshed(user.Id, presented.FamilyId);

        return AuthenticationResult.From(user, token, refresh.Token);
    }

    // Runs to the end even if the client disconnects. Otherwise cancelling the request would be a way to stop a
    // stolen family from being revoked.
    // Returns how many tokens were still live, which is what the log reports: the ones a rotation had already revoked
    // were dead before the reuse.
    private async Task<int> RevokeFamilyAsync(Guid familyId, DateTime now)
    {
        var family = await _refreshTokenRepository.GetFamilyAsync(familyId, CancellationToken.None);
        var stillLive = 0;
        foreach (var token in family)
        {
            if (!token.IsRevoked)
                stillLive++;

            token.Revoke(now);
        }

        await _unitOfWork.SaveChangesAsync(CancellationToken.None);

        return stillLive;
    }
}
