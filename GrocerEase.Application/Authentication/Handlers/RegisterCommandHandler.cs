using GrocerEase.Application.Authentication.Commands;
using GrocerEase.Application.Common.Handlers;
using GrocerEase.Application.Common.Interfaces.Authentication;
using GrocerEase.Application.Common.Interfaces.Persistence;
using GrocerEase.Domain.Entities;
using GrocerEase.Domain.Exceptions;

namespace GrocerEase.Application.Authentication.Handlers;

public sealed class RegisterCommandHandler : ICommandHandler<RegisterCommand, AuthenticationResult>
{
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterCommandHandler(
        IJwtTokenGenerator jwtTokenGenerator,
        IPasswordHasher passwordHasher,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordHasher = passwordHasher;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<AuthenticationResult> HandleAsync(RegisterCommand command, CancellationToken cancellationToken = default)
    {
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
            PasswordHash = _passwordHasher.Hash(command.Password),
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.FirstName, user.LastName, user.Email);

        return AuthenticationResult.From(user, token);
    }
}
