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

    public AuthenticationResult Register(RegisterCommand command)
    {
        _registerValidator.ValidateAndThrow(command);

        var user = new User
        {
            Id = Guid.CreateVersion7(),
            FirstName = command.FirstName,
            LastName = command.LastName,
            Email = command.Email.ToLowerInvariant(),
            Password = _passwordHasher.Hash(command.Password),
        };

        _userRepository.Add(user);

        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.FirstName, user.LastName);

        return AuthenticationResult.From(user, token);
    }

    public AuthenticationResult Login(LoginCommand command)
    {
        _loginValidator.ValidateAndThrow(command);

        var user = _userRepository.GetUserByEmail(command.Email.ToLowerInvariant());

        if (user is null || !_passwordHasher.Verify(command.Password, user.Password))
        {
            throw new InvalidCredentialsException();
        }

        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.FirstName, user.LastName);

        return AuthenticationResult.From(user, token);
    }
}
