using GrocerEase.Application.Authentication.Commands;
using GrocerEase.Application.Common.Handlers;
using GrocerEase.Application.Common.Interfaces.Authentication;
using GrocerEase.Application.Common.Interfaces.Persistence;
using GrocerEase.Application.Common.Interfaces.Services;

namespace GrocerEase.Application.Authentication.Handlers;

public sealed class LogoutCommandHandler : ICommandHandler<LogoutCommand, Unit>
{
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public LogoutCommandHandler(
        IRefreshTokenGenerator refreshTokenGenerator,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock)
    {
        _refreshTokenGenerator = refreshTokenGenerator;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Unit> HandleAsync(LogoutCommand command, CancellationToken cancellationToken = default)
    {
        var hash = _refreshTokenGenerator.Hash(command.RefreshToken);
        var presented = await _refreshTokenRepository.GetByHashAsync(hash, cancellationToken);

        // An unknown token is a success too. Logging out twice is harmless, and a different answer would tell a caller
        // which tokens exist.
        if (presented is null)
        {
            return Unit.Value;
        }

        // The whole family, so the newest token stops working and not only the one that was presented.
        var now = _clock.UtcNow;
        foreach (var token in await _refreshTokenRepository.GetFamilyAsync(presented.FamilyId, cancellationToken))
        {
            token.Revoke(now);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
