using GrocerEase.Application.Authentication.Commands;

namespace GrocerEase.Application.Authentication;

public interface IAuthenticationService
{
    Task<AuthenticationResult> RegisterAsync(RegisterCommand command, CancellationToken cancellationToken = default);
    Task<AuthenticationResult> LoginAsync(LoginCommand command, CancellationToken cancellationToken = default);
}
