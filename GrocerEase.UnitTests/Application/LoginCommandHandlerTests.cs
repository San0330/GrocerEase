using GrocerEase.Application.Authentication.Commands;
using GrocerEase.Application.Authentication.Handlers;
using GrocerEase.Application.Common.Interfaces.Authentication;
using GrocerEase.Application.Common.Interfaces.Persistence;
using GrocerEase.Domain.Entities;
using GrocerEase.Domain.Exceptions;
using Moq;

namespace GrocerEase.UnitTests.Application;

public class LoginCommandHandlerTests
{
    private readonly Mock<IJwtTokenGenerator> _jwt = new();
    private readonly Mock<IPasswordHasher> _passwords = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly LoginCommandHandler _sut;

    public LoginCommandHandlerTests()
    {
        _sut = new LoginCommandHandler(_jwt.Object, _passwords.Object, _users.Object);
    }

    [Fact]
    public async Task WithMatchingPassword_ReturnsTokenForThatUser()
    {
        var user = ExistingUser();
        _users
            .Setup(r => r.GetUserByEmailAsync("ada@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwords.Setup(p => p.Verify("Passw0rd!", user.PasswordHash)).Returns(true);
        _jwt
            .Setup(j => j.GenerateToken(user.Id, user.FirstName, user.LastName, user.Email))
            .Returns("signed-token");

        var result = await _sut.HandleAsync(new LoginCommand("Ada@Example.COM", "Passw0rd!"));

        Assert.Equal(user.Id, result.Id);
        Assert.Equal(user.Email, result.Email);
        Assert.Equal("signed-token", result.Token);
        _users.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ForwardsCancellationTokenToTheRepository()
    {
        using var cts = new CancellationTokenSource();
        var user = ExistingUser();
        _users.Setup(r => r.GetUserByEmailAsync("ada@example.com", cts.Token)).ReturnsAsync(user);
        _passwords.Setup(p => p.Verify(It.IsAny<string>(), It.IsAny<string?>())).Returns(true);
        _jwt.Setup(j => j.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("token");

        await _sut.HandleAsync(new LoginCommand("ada@example.com", "Passw0rd!"), cts.Token);

        _users.Verify(r => r.GetUserByEmailAsync("ada@example.com", cts.Token), Times.Once);
    }

    [Fact]
    public async Task WhenEmailIsUnknown_StillChecksAPasswordSoTheAnswerTakesAsLong()
    {
        _users
            .Setup(r => r.GetUserByEmailAsync("ada@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var exception = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _sut.HandleAsync(new LoginCommand("Ada@Example.COM", "Passw0rd!")));

        Assert.Equal("Email or password is invalid.", exception.ErrorMessage);
        _passwords.Verify(p => p.Verify("Passw0rd!", null), Times.Once);
        _jwt.Verify(
            j => j.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task WhenPasswordDoesNotMatch_RejectsWithTheSameErrorAsAnUnknownEmail()
    {
        var user = ExistingUser();
        _users
            .Setup(r => r.GetUserByEmailAsync("ada@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwords.Setup(p => p.Verify("wrong-password", user.PasswordHash)).Returns(false);

        var exception = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _sut.HandleAsync(new LoginCommand("ada@example.com", "wrong-password")));

        Assert.Equal("Email or password is invalid.", exception.ErrorMessage);
        _jwt.Verify(
            j => j.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    private static User ExistingUser() => new()
    {
        Id = Guid.Parse("018f6b2e-7c3a-7a1b-8c2d-4e5f60718293"),
        FirstName = "Ada",
        LastName = "Lovelace",
        Email = "ada@example.com",
        PasswordHash = "stored-hash",
    };
}
