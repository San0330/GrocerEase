using GrocerEase.Api.Controllers;
using GrocerEase.Application.Authentication;
using GrocerEase.Application.Authentication.Commands;
using GrocerEase.Application.Common.Handlers;
using GrocerEase.Contracts.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GrocerEase.UnitTests.Api;

public class AuthenticationControllerTests
{
    private readonly Mock<ICommandHandler<RegisterCommand, AuthenticationResult>> _register = new();
    private readonly Mock<ICommandHandler<LoginCommand, AuthenticationResult>> _login = new();
    private readonly Mock<ICommandHandler<RefreshTokenCommand, AuthenticationResult>> _refresh = new();
    private readonly Mock<ICommandHandler<LogoutCommand, Unit>> _logout = new();
    private readonly AuthenticationController _controller;

    public AuthenticationControllerTests()
    {
        _controller = new AuthenticationController(_register.Object, _login.Object, _refresh.Object, _logout.Object);
    }

    [Fact]
    public async Task Register_ReturnsCreatedWithTheAuthenticationResponse()
    {
        var id = Guid.Parse("018f6b2e-7c3a-7a1b-8c2d-4e5f60718293");
        using var cts = new CancellationTokenSource();
        _register
            .Setup(s => s.HandleAsync(
                It.Is<RegisterCommand>(c =>
                    c.FirstName == "Ada" &&
                    c.LastName == "Lovelace" &&
                    c.Email == "Ada@Example.COM" &&
                    c.Password == "Passw0rd!"),
                cts.Token))
            .ReturnsAsync(new AuthenticationResult(id, "Ada", "Lovelace", "ada@example.com", "signed-token", "refresh-token"));

        var result = await _controller.Register(
            new RegisterRequest("Ada", "Lovelace", "Ada@Example.COM", "Passw0rd!"),
            cts.Token);

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        var body = Assert.IsType<AuthenticationResponse>(created.Value);
        Assert.Equal(id, body.Id);
        Assert.Equal("Ada", body.FirstName);
        Assert.Equal("Lovelace", body.LastName);
        Assert.Equal("ada@example.com", body.Email);
        Assert.Equal("signed-token", body.Token);
        Assert.Equal("refresh-token", body.RefreshToken);
    }

    [Fact]
    public async Task Login_ReturnsTheAuthenticationResponse()
    {
        var id = Guid.Parse("018f6b2e-7c3a-7a1b-8c2d-4e5f60718293");
        using var cts = new CancellationTokenSource();
        _login
            .Setup(s => s.HandleAsync(
                It.Is<LoginCommand>(c => c.Email == "ada@example.com" && c.Password == "Passw0rd!"),
                cts.Token))
            .ReturnsAsync(new AuthenticationResult(id, "Ada", "Lovelace", "ada@example.com", "signed-token", "refresh-token"));

        var result = await _controller.Login(
            new LoginRequest("ada@example.com", "Passw0rd!"),
            cts.Token);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<AuthenticationResponse>(ok.Value);
        Assert.Equal(id, body.Id);
        Assert.Equal("signed-token", body.Token);
        Assert.Equal("refresh-token", body.RefreshToken);
    }

    [Fact]
    public async Task Refresh_ReturnsTheNewPairInAnAuthenticationResponse()
    {
        var id = Guid.Parse("018f6b2e-7c3a-7a1b-8c2d-4e5f60718293");
        using var cts = new CancellationTokenSource();
        _refresh
            .Setup(s => s.HandleAsync(It.Is<RefreshTokenCommand>(c => c.RefreshToken == "old-refresh"), cts.Token))
            .ReturnsAsync(new AuthenticationResult(id, "Ada", "Lovelace", "ada@example.com", "new-jwt", "new-refresh"));

        var result = await _controller.Refresh(new RefreshRequest("old-refresh"), cts.Token);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<AuthenticationResponse>(ok.Value);
        Assert.Equal(id, body.Id);
        Assert.Equal("new-jwt", body.Token);
        Assert.Equal("new-refresh", body.RefreshToken);
    }

    [Fact]
    public async Task Logout_ReturnsNoContent_AfterHandingTheTokenToTheHandler()
    {
        using var cts = new CancellationTokenSource();
        _logout
            .Setup(s => s.HandleAsync(It.Is<LogoutCommand>(c => c.RefreshToken == "some-refresh"), cts.Token))
            .ReturnsAsync(Unit.Value);

        var result = await _controller.Logout(new LogoutRequest("some-refresh"), cts.Token);

        Assert.IsType<NoContentResult>(result);
        _logout.VerifyAll();
    }
}
