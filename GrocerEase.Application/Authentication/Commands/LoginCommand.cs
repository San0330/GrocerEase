namespace GrocerEase.Application.Authentication.Commands;

public record LoginCommand(
    string Email,
    string Password);
