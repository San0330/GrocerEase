using GrocerEase.Application.Authentication.Commands;
using GrocerEase.Application.Common.Handlers;
using GrocerEase.Application.Common.Interfaces.Authentication;
using GrocerEase.Application.Common.Interfaces.Persistence;
using GrocerEase.Application.Common.Interfaces.Services;
using GrocerEase.Domain.Entities;
using GrocerEase.Domain.Exceptions;
using GrocerEase.Domain.ValueObjects;

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

    public LoginCommandHandler(
        IJwtTokenGenerator jwtTokenGenerator,
        IPasswordHasher passwordHasher,
        IUserRepository userRepository,
        IRefreshTokenGenerator refreshTokenGenerator,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock)
    {
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordHasher = passwordHasher;
        _userRepository = userRepository;
        _refreshTokenGenerator = refreshTokenGenerator;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<AuthenticationResult> HandleAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetUserByEmailAsync(Email.Create(command.Email), cancellationToken);

        // Verify even when there is no user, so a wrong email is not answered faster than a wrong password.
        var passwordMatches = _passwordHasher.Verify(command.Password, user?.PasswordHash);

        if (user is null || !passwordMatches)
        {
            throw new InvalidCredentialsException();
        }

        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.FirstName, user.LastName, user.Email.Value);

        // Each login starts its own family, so signing out of one device does not sign out the others.
        var refresh = _refreshTokenGenerator.Issue();
        await _refreshTokenRepository.AddAsync(
            RefreshToken.StartFamily(user.Id, refresh.Hash, _clock.UtcNow, refresh.ExpiresAt),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return AuthenticationResult.From(user, token, refresh.Token);
    }
}
