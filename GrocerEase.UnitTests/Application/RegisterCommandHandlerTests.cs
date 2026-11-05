using GrocerEase.Application.Authentication;
using GrocerEase.Application.Authentication.Commands;
using GrocerEase.Application.Authentication.Handlers;
using GrocerEase.Application.Common.Interfaces.Authentication;
using GrocerEase.Application.Common.Interfaces.Persistence;
using GrocerEase.Domain.Entities;
using GrocerEase.Domain.Exceptions;
using GrocerEase.Domain.ValueObjects;
using GrocerEase.UnitTests.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;

namespace GrocerEase.UnitTests.Application;

public sealed class RegisterCommandHandlerTests : IDisposable
{
    private readonly Mock<IJwtTokenGenerator> _jwt = new();
    private readonly Mock<IPasswordHasher> _passwords = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IRefreshTokenGenerator> _refreshTokens = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenStore = new();
    private readonly FakeLogger<RegisterCommandHandler> _log = new();
    private readonly TestMetrics _metrics = new();
    private static readonly DateTime Now = new(2026, 11, 1, 9, 30, 0, DateTimeKind.Utc);
    private static readonly IssuedRefreshToken Issued = new("raw-refresh-token", "refresh-hash", Now.AddDays(30));

    private readonly RegisterCommandHandler _sut;

    public RegisterCommandHandlerTests()
    {
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _refreshTokens.Setup(g => g.Issue()).Returns(Issued);

        _sut = new RegisterCommandHandler(
            _jwt.Object,
            _passwords.Object,
            _users.Object,
            _unitOfWork.Object,
            new FixedDateTimeProvider(Now),
            _refreshTokens.Object,
            _refreshTokenStore.Object,
            _log,
            _metrics.Metrics);
    }

    public void Dispose() => _metrics.Dispose();

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
        Assert.Equal("ada@example.com", saved.Email.Value);
        Assert.Equal(hash, saved.PasswordHash);
        Assert.Equal(Now, saved.CreatedAt);
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
    public async Task WithValidCommand_ReturnsARefreshTokenStoredHashedAndSavedWithTheUserInOneCommit()
    {
        var order = new List<string>();
        User? savedUser = null;
        RefreshToken? savedToken = null;
        _passwords.Setup(p => p.Hash(It.IsAny<string>())).Returns("hashed");
        _jwt.Setup(j => j.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("token");
        _users
            .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => { savedUser = user; order.Add("user"); })
            .Returns(Task.CompletedTask);
        _refreshTokenStore
            .Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshToken, CancellationToken>((token, _) => { savedToken = token; order.Add("token"); })
            .Returns(Task.CompletedTask);
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("save"))
            .Returns(Task.CompletedTask);

        var result = await _sut.HandleAsync(ValidRegister());

        Assert.Equal("raw-refresh-token", result.RefreshToken);
        Assert.NotNull(savedToken);
        Assert.Equal(savedUser!.Id, savedToken.UserId);
        Assert.Equal("refresh-hash", savedToken.TokenHash);
        Assert.Equal(Now, savedToken.CreatedAt);
        Assert.Equal(Now.AddDays(30), savedToken.ExpiresAt);
        // One save, so the user and the first token are stored together or not at all.
        Assert.Equal(["user", "token", "save"], order);
    }

    [Fact]
    public async Task ForwardsCancellationTokenToTheRepository()
    {
        using var cts = new CancellationTokenSource();
        _passwords.Setup(p => p.Hash(It.IsAny<string>())).Returns("hashed");
        _jwt.Setup(j => j.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("token");

        await _sut.HandleAsync(ValidRegister(), cts.Token);

        _users.Verify(r => r.GetUserByEmailAsync(Email.Create("ada@example.com"), cts.Token), Times.Once);
        _users.Verify(r => r.AddAsync(It.IsAny<User>(), cts.Token), Times.Once);
        _refreshTokenStore.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), cts.Token), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(cts.Token), Times.Once);
    }

    [Fact]
    public async Task WhenEmailAlreadyExists_DoesNotCreateAnotherUser()
    {
        _users
            .Setup(r => r.GetUserByEmailAsync(Email.Create("ada@example.com"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExistingUser());

        var exception = await Assert.ThrowsAsync<DuplicateEmailException>(() =>
            _sut.HandleAsync(ValidRegister()));

        Assert.Equal(ErrorKind.Conflict, exception.Kind);
        Assert.Equal(ErrorCodes.DuplicateEmail, exception.ErrorCode);
        Assert.Equal("A user with this email already exists.", exception.ErrorMessage);
        _passwords.Verify(p => p.Hash(It.IsAny<string>()), Times.Never);
        _users.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokens.Verify(g => g.Issue(), Times.Never);
        _jwt.Verify(
            j => j.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task WithValidCommand_LogsWhoRegistered_AfterTheSave()
    {
        var order = new List<string>();
        User? saved = null;
        _passwords.Setup(p => p.Hash(It.IsAny<string>())).Returns("hashed");
        _jwt.Setup(j => j.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("token");
        _users
            .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => saved = user)
            .Returns(Task.CompletedTask);
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => order.Add(_log.Collector.Count == 0 ? "saved-before-log" : "saved-after-log"))
            .Returns(Task.CompletedTask);

        await _sut.HandleAsync(ValidRegister());

        // The event says the user exists, so it must not be written for a registration that then fails to save.
        Assert.Equal(["saved-before-log"], order);
        var record = Assert.Single(_log.Collector.GetSnapshot());
        Assert.Equal(1001, record.Id.Id);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(saved!.Id.ToString(), record.StructuredState!.Single(p => p.Key == "UserId").Value);
    }

    [Fact]
    public async Task WhenTheSaveFails_LogsNothing()
    {
        _passwords.Setup(p => p.Hash(It.IsAny<string>())).Returns("hashed");
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateEmailException());

        await Assert.ThrowsAsync<DuplicateEmailException>(() => _sut.HandleAsync(ValidRegister()));

        Assert.Empty(_log.Collector.GetSnapshot());
    }

    [Fact]
    public async Task WhenTheEmailIsAlreadyRegistered_LogsTheRejectionWithAFingerprintAndNotTheAddress()
    {
        _users
            .Setup(r => r.GetUserByEmailAsync(Email.Create("ada@example.com"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExistingUser());

        await Assert.ThrowsAsync<DuplicateEmailException>(() => _sut.HandleAsync(ValidRegister()));

        var record = Assert.Single(_log.Collector.GetSnapshot());
        Assert.Equal(1002, record.Id.Id);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(EmailFingerprint.Of("ada@example.com"), record.StructuredState!.Single(p => p.Key == "EmailFingerprint").Value);
        var everything = record.Message + string.Join(' ', record.StructuredState!.Select(pair => pair.Value));
        Assert.DoesNotContain("ada@example.com", everything, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Passw0rd!", everything);
    }

    [Fact]
    public async Task WithValidCommand_CountsASuccessfulRegistration()
    {
        var attempts = _metrics.Count("grocerease.auth.attempts");
        _passwords.Setup(p => p.Hash(It.IsAny<string>())).Returns("hashed");
        _jwt.Setup(j => j.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns("token");

        await _sut.HandleAsync(ValidRegister());

        Assert.Equal(["register/success"], TestMetrics.Attempts(attempts));
    }

    [Fact]
    public async Task WhenTheEmailIsAlreadyRegistered_CountsAFailedRegistration()
    {
        var attempts = _metrics.Count("grocerease.auth.attempts");
        _users
            .Setup(r => r.GetUserByEmailAsync(Email.Create("ada@example.com"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExistingUser());

        await Assert.ThrowsAsync<DuplicateEmailException>(() => _sut.HandleAsync(ValidRegister()));

        Assert.Equal(["register/failure"], TestMetrics.Attempts(attempts));
    }

    private static RegisterCommand ValidRegister() =>
        new("Ada", "Lovelace", "Ada@Example.COM", "Passw0rd!");

    private static User ExistingUser() => User.Register(
        "Ada",
        "Lovelace",
        Email.Create("ada@example.com"),
        "stored-hash",
        new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc));
}
