using FluentValidation;
using GrocerEase.Application.Authentication.Commands;
using GrocerEase.Application.Common.Interfaces.Authentication;
using GrocerEase.Application.Common.Interfaces.Persistence;
using GrocerEase.Domain.Entities;
using GrocerEase.Domain.Exceptions;

namespace GrocerEase.Application.Authentication;

public class AuthenticationService : IAuthenticationService
{
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUserRepository _userRepository;
    private readonly IValidator<RegisterCommand> _registerValidator;
    private readonly IValidator<LoginCommand> _loginValidator;

    public AuthenticationService(
        IJwtTokenGenerator jwtTokenGenerator,
        IPasswordHasher passwordHasher,
        IUserRepository userRepository,
        IValidator<RegisterCommand> registerValidator,
        IValidator<LoginCommand> loginValidator)
    {
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordHasher = passwordHasher;
        _userRepository = userRepository;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
    }

    public async Task<AuthenticationResult> RegisterAsync(RegisterCommand command, CancellationToken cancellationToken = default)
    {
        await _registerValidator.ValidateAndThrowAsync(command, cancellationToken);

        var email = command.Email.ToLowerInvariant();
        var existing = await _userRepository.GetUserByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            throw new DuplicateEmailException();
        }

        var user = new User
        {
            Id = Guid.CreateVersion7(),
            FirstName = command.FirstName,
            LastName = command.LastName,
            Email = email,
            Password = _passwordHasher.Hash(command.Password),
        };

        await _userRepository.AddAsync(user, cancellationToken);

        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.FirstName, user.LastName, user.Email);

        return AuthenticationResult.From(user, token);
    }

    public async Task<AuthenticationResult> LoginAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        await _loginValidator.ValidateAndThrowAsync(command, cancellationToken);

        var user = await _userRepository.GetUserByEmailAsync(command.Email.ToLowerInvariant(), cancellationToken);

        if (user is null || !_passwordHasher.Verify(command.Password, user.Password))
        {
            throw new InvalidCredentialsException();
        }

        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.FirstName, user.LastName, user.Email);

        return AuthenticationResult.From(user, token);
    }
}
