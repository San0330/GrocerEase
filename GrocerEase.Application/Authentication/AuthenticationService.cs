using GrocerEase.Application.Common.Interfaces.Persistence;
using GrocerEase.Domain.Entities;

namespace GrocerEase.Application.Authentication;

public class AuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _userRepository;

    public AuthenticationService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public AuthenticationResult Register(string firstName, string lastName, string email, string password)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Password = password, // TODO: hash password before storing
        };

        _userRepository.Add(user);

        return new AuthenticationResult(user.Id, user.FirstName, user.LastName, user.Email, "token");
    }

    public AuthenticationResult Login(string email, string password)
    {
        var user = _userRepository.GetUserByEmail(email);

        if (user is null)
        {
            throw new Exception("User not found.");
        }

        return new AuthenticationResult(user.Id, user.FirstName, user.LastName, user.Email, "token");
    }
}
