using GrocerEase.Application.Authentication.Commands;
using GrocerEase.Application.Authentication.Handlers;
using GrocerEase.Application.Common.Interfaces.Authentication;
using GrocerEase.Application.Common.Interfaces.Persistence;
using GrocerEase.Domain.Entities;
using GrocerEase.Domain.Exceptions;
using Moq;

namespace GrocerEase.UnitTests.Application;

public class RegisterCommandHandlerTests
{
    private readonly Mock<IJwtTokenGenerator> _jwt = new();
    private readonly Mock<IPasswordHasher> _passwords = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly RegisterCommandHandler _sut;

    public RegisterCommandHandlerTests()
    {
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new RegisterCommandHandler(_jwt.Object, _passwords.Object, _users.Object, _unitOfWork.Object);
    }

    [Fact]
    public async Task WithValidCommand_StoresLowercasedEmailAndHashedPassword()
    {
        const string hash = "hashed-password";
        const string token = "signed-token";
        User? saved = null;

        _passwords.Setup(p => p.Hash("Passw0rd!")).Returns(hash);
        _users
            .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => saved = user)
            .Returns(Task.CompletedTask);
        _jwt
            .Setup(j => j.GenerateToken(It.IsAny<Guid>(), "Ada", "Lovelace", "ada@example.com"))
            .Returns(token);

        var result = await _sut.HandleAsync(ValidRegister());

        Assert.NotNull(saved);
        Assert.Equal("Ada", saved.FirstName);
        Assert.Equal("Lovelace", saved.LastName);
        Assert.Equal("ada@example.com", saved.Email);
        Assert.Equal(hash, saved.PasswordHash);
        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.Equal(7, saved.Id.Version);
        Assert.Equal(saved.Id, result.Id);
        Assert.Equal("ada@example.com", result.Email);
        Assert.Equal(token, result.Token);
        _jwt.Verify(
            j => j.GenerateToken(saved.Id, "Ada", "Lovelace", "ada@example.com"),
            Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForwardsCancellationTokenToTheRepository()
    {
        using var cts = new CancellationTokenSource();
        _passwords.Setup(p => p.Hash(It.IsAny<string>())).Returns("hashed");
        _jwt.Setup(j => j.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("token");

        await _sut.HandleAsync(ValidRegister(), cts.Token);

        _users.Verify(r => r.GetUserByEmailAsync("ada@example.com", cts.Token), Times.Once);
        _users.Verify(r => r.AddAsync(It.IsAny<User>(), cts.Token), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(cts.Token), Times.Once);
    }

    [Fact]
    public async Task WhenEmailAlreadyExists_DoesNotCreateAnotherUser()
    {
        _users
            .Setup(r => r.GetUserByEmailAsync("ada@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExistingUser());

        var exception = await Assert.ThrowsAsync<DuplicateEmailException>(() =>
            _sut.HandleAsync(ValidRegister()));

        Assert.Equal(ErrorKind.Conflict, exception.Kind);
        Assert.Equal(ErrorCodes.DuplicateEmail, exception.ErrorCode);
        Assert.Equal("A user with this email already exists.", exception.ErrorMessage);
        _passwords.Verify(p => p.Hash(It.IsAny<string>()), Times.Never);
        _users.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _jwt.Verify(
            j => j.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    private static RegisterCommand ValidRegister() =>
        new("Ada", "Lovelace", "Ada@Example.COM", "Passw0rd!");

    private static User ExistingUser() => new()
    {
        Id = Guid.Parse("018f6b2e-7c3a-7a1b-8c2d-4e5f60718293"),
        FirstName = "Ada",
        LastName = "Lovelace",
        Email = "ada@example.com",
        PasswordHash = "stored-hash",
    };
}
