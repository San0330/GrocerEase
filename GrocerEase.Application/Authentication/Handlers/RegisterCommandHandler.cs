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

public sealed class RegisterCommandHandler : ICommandHandler<RegisterCommand, AuthenticationResult>
{
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILogger<RegisterCommandHandler> _logger;
    private readonly GrocerEaseMetrics _metrics;

    public RegisterCommandHandler(
        IJwtTokenGenerator jwtTokenGenerator,
        IPasswordHasher passwordHasher,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IRefreshTokenGenerator refreshTokenGenerator,
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<RegisterCommandHandler> logger,
        GrocerEaseMetrics metrics)
    {
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordHasher = passwordHasher;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _refreshTokenGenerator = refreshTokenGenerator;
        _refreshTokenRepository = refreshTokenRepository;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<AuthenticationResult> HandleAsync(RegisterCommand command, CancellationToken cancellationToken = default)
    {
        var email = Email.Create(command.Email);
        var existing = await _userRepository.GetUserByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            _logger.RegistrationRejected(EmailFingerprint.Of(command.Email));
            _metrics.AuthAttempt(GrocerEaseMetrics.Operations.Register, succeeded: false);
            throw new DuplicateEmailException();
        }

        var user = User.Register(
            command.FirstName,
            command.LastName,
            email,
            _passwordHasher.Hash(command.Password),
            _clock.UtcNow);

        var refresh = _refreshTokenGenerator.Issue();

        await _userRepository.AddAsync(user, cancellationToken);
        await _refreshTokenRepository.AddAsync(
            RefreshToken.StartFamily(user.Id, refresh.Hash, _clock.UtcNow, refresh.ExpiresAt),
            cancellationToken);

        // One save, so the user and their first token are stored together or not at all.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // After the save: the event says the user exists, so a registration that fails to store must not claim it.
        _logger.UserRegistered(user.Id);
        _metrics.AuthAttempt(GrocerEaseMetrics.Operations.Register, succeeded: true);

        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.FirstName, user.LastName, user.Email.Value);

        return AuthenticationResult.From(user, token, refresh.Token);
    }
}
