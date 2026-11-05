using GrocerEase.Application.Authentication.Commands;
using GrocerEase.Application.Common.Handlers;
using GrocerEase.Application.Common.Interfaces.Authentication;
using GrocerEase.Application.Common.Interfaces.Persistence;
using GrocerEase.Application.Common.Interfaces.Services;
using GrocerEase.Domain.Exceptions;

namespace GrocerEase.Application.Authentication.Handlers;

public sealed class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand, AuthenticationResult>
{
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public RefreshTokenCommandHandler(
        IJwtTokenGenerator jwtTokenGenerator,
        IRefreshTokenGenerator refreshTokenGenerator,
        IRefreshTokenRepository refreshTokenRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock)
    {
        _jwtTokenGenerator = jwtTokenGenerator;
        _refreshTokenGenerator = refreshTokenGenerator;
        _refreshTokenRepository = refreshTokenRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<AuthenticationResult> HandleAsync(RefreshTokenCommand command, CancellationToken cancellationToken = default)
    {
        var hash = _refreshTokenGenerator.Hash(command.RefreshToken);
        var presented = await _refreshTokenRepository.GetByHashAsync(hash, cancellationToken)
                        ?? throw new InvalidRefreshTokenException();

        var now = _clock.UtcNow;

        if (presented.IsRevoked)
        {
            // Every use revokes the token it replaces, so a revoked token coming back means a copy exists somewhere.
            // The owner and the holder of the copy cannot be told apart, so the whole family goes.
            await RevokeFamilyAsync(presented.FamilyId, now);
            throw new InvalidRefreshTokenException();
        }

        if (presented.IsExpired(now))
        {
            throw new InvalidRefreshTokenException();
        }

        var user = await _userRepository.GetByIdAsync(presented.UserId, cancellationToken)
                   ?? throw new InvalidRefreshTokenException();

        var refresh = _refreshTokenGenerator.Issue();
        var successor = presented.Rotate(refresh.Hash, now, refresh.ExpiresAt);
        await _refreshTokenRepository.AddAsync(successor, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.FirstName, user.LastName, user.Email.Value);

        return AuthenticationResult.From(user, token, refresh.Token);
    }

    // Runs to the end even if the client disconnects. Otherwise cancelling the request would be a way to stop a
    // stolen family from being revoked.
    private async Task RevokeFamilyAsync(Guid familyId, DateTime now)
    {
        var family = await _refreshTokenRepository.GetFamilyAsync(familyId, CancellationToken.None);
        foreach (var token in family)
        {
            token.Revoke(now);
        }

        await _unitOfWork.SaveChangesAsync(CancellationToken.None);
    }
}
