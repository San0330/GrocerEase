using GrocerEase.Application.Authentication.Commands;

namespace GrocerEase.Application.Authentication;

public interface IAuthenticationService
{
    AuthenticationResult Register(RegisterCommand command);
    AuthenticationResult Login(LoginCommand command);
}
