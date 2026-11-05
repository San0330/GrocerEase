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

public sealed class LoginCommandHandlerTests : IDisposable
{
    private readonly Mock<IJwtTokenGenerator> _jwt = new();
    private readonly Mock<IPasswordHasher> _passwords = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRefreshTokenGenerator> _refreshTokens = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenStore = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeLogger<LoginCommandHandler> _log = new();
    private readonly TestMetrics _metrics = new();
    private static readonly DateTime Now = new(2026, 11, 5, 9, 30, 0, DateTimeKind.Utc);
    private static readonly IssuedRefreshToken Issued = new("raw-refresh-token", "refresh-hash", Now.AddDays(30));
    private readonly LoginCommandHandler _sut;

    public LoginCommandHandlerTests()
    {
        _refreshTokens.Setup(g => g.Issue()).Returns(Issued);
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new LoginCommandHandler(
            _jwt.Object,
            _passwords.Object,
            _users.Object,
            _refreshTokens.Object,
            _refreshTokenStore.Object,
            _unitOfWork.Object,
            new FixedDateTimeProvider(Now),
            _log,
            _metrics.Metrics);
    }

    public void Dispose() => _metrics.Dispose();

    [Fact]
    public async Task WithMatchingPassword_ReturnsTokenForThatUser()
    {
        var user = ExistingUser();
        _users
            .Setup(r => r.GetUserByEmailAsync(Email.Create("ada@example.com"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwords.Setup(p => p.Verify("Passw0rd!", user.PasswordHash)).Returns(true);
        _jwt
            .Setup(j => j.GenerateToken(user.Id, user.FirstName, user.LastName, user.Email.Value))
            .Returns("signed-token");

        var result = await _sut.HandleAsync(new LoginCommand("Ada@Example.COM", "Passw0rd!"));

        Assert.Equal(user.Id, result.Id);
        Assert.Equal(user.Email.Value, result.Email);
        Assert.Equal("signed-token", result.Token);
        _users.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WithMatchingPassword_ReturnsTheRefreshTokenAndStoresOnlyItsHashInANewFamily()
    {
        var user = ExistingUser();
        RefreshToken? stored = null;
        GivenLoginSucceeds(user);
        _refreshTokenStore
            .Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshToken, CancellationToken>((token, _) => stored = token)
            .Returns(Task.CompletedTask);

        var result = await _sut.HandleAsync(new LoginCommand("ada@example.com", "Passw0rd!"));

        Assert.Equal("raw-refresh-token", result.RefreshToken);
        Assert.NotNull(stored);
        Assert.Equal(user.Id, stored.UserId);
        Assert.Equal("refresh-hash", stored.TokenHash);
        Assert.Equal(Now, stored.CreatedAt);
        Assert.Equal(Now.AddDays(30), stored.ExpiresAt);
        Assert.False(stored.IsRevoked);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EachLogin_StartsItsOwnFamily()
    {
        var families = new List<Guid>();
        GivenLoginSucceeds(ExistingUser());
        _refreshTokenStore
            .Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshToken, CancellationToken>((token, _) => families.Add(token.FamilyId))
            .Returns(Task.CompletedTask);

        await _sut.HandleAsync(new LoginCommand("ada@example.com", "Passw0rd!"));
        await _sut.HandleAsync(new LoginCommand("ada@example.com", "Passw0rd!"));

        Assert.Equal(2, families.Distinct().Count());
    }

    [Fact]
    public async Task ForwardsCancellationTokenToTheRepository()
    {
        using var cts = new CancellationTokenSource();
        var user = ExistingUser();
        _users.Setup(r => r.GetUserByEmailAsync(Email.Create("ada@example.com"), cts.Token)).ReturnsAsync(user);
        _passwords.Setup(p => p.Verify(It.IsAny<string>(), It.IsAny<string?>())).Returns(true);
        _jwt.Setup(j => j.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("token");

        await _sut.HandleAsync(new LoginCommand("ada@example.com", "Passw0rd!"), cts.Token);

        _users.Verify(r => r.GetUserByEmailAsync(Email.Create("ada@example.com"), cts.Token), Times.Once);
        _refreshTokenStore.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), cts.Token), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(cts.Token), Times.Once);
    }

    [Fact]
    public async Task WhenEmailIsUnknown_StillChecksAPasswordSoTheAnswerTakesAsLong()
    {
        _users
            .Setup(r => r.GetUserByEmailAsync(Email.Create("ada@example.com"), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var exception = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _sut.HandleAsync(new LoginCommand("Ada@Example.COM", "Passw0rd!")));

        Assert.Equal("Email or password is invalid.", exception.ErrorMessage);
        _passwords.Verify(p => p.Verify("Passw0rd!", null), Times.Once);
        AssertNothingIssuedOrSaved();
        _jwt.Verify(
            j => j.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task WhenPasswordDoesNotMatch_RejectsWithTheSameErrorAsAnUnknownEmail()
    {
        var user = ExistingUser();
        _users
            .Setup(r => r.GetUserByEmailAsync(Email.Create("ada@example.com"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwords.Setup(p => p.Verify("wrong-password", user.PasswordHash)).Returns(false);

        var exception = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _sut.HandleAsync(new LoginCommand("ada@example.com", "wrong-password")));

        Assert.Equal("Email or password is invalid.", exception.ErrorMessage);
        _jwt.Verify(
            j => j.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        AssertNothingIssuedOrSaved();
    }

    [Fact]
    public async Task WhenLoginSucceeds_LogsWhoLoggedIn()
    {
        var user = ExistingUser();
        GivenLoginSucceeds(user);

        await _sut.HandleAsync(new LoginCommand("ada@example.com", "Passw0rd!"));

        var record = Assert.Single(_log.Collector.GetSnapshot());
        Assert.Equal(1003, record.Id.Id);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(user.Id.ToString(), State(record, "UserId"));
    }

    [Fact]
    public async Task WhenTheEmailIsUnknown_LogsAWarningWithTheReasonAndOnlyAFingerprintOfTheEmail()
    {
        _users
            .Setup(r => r.GetUserByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _sut.HandleAsync(new LoginCommand("Ada@Example.COM", "Passw0rd!")));

        var record = Assert.Single(_log.Collector.GetSnapshot());
        Assert.Equal(1004, record.Id.Id);
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal("unknown_email", State(record, "Reason"));
        Assert.Equal(EmailFingerprint.Of("ada@example.com"), State(record, "EmailFingerprint"));
        AssertNothingSensitiveWasLogged(record);
    }

    [Fact]
    public async Task WhenThePasswordIsWrong_LogsAWarningWithADifferentReasonFromAnUnknownEmail()
    {
        var user = ExistingUser();
        _users
            .Setup(r => r.GetUserByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwords.Setup(p => p.Verify("wrong-password", user.PasswordHash)).Returns(false);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _sut.HandleAsync(new LoginCommand("ada@example.com", "wrong-password")));

        var record = Assert.Single(_log.Collector.GetSnapshot());
        Assert.Equal(1004, record.Id.Id);
        Assert.Equal("wrong_password", State(record, "Reason"));
        AssertNothingSensitiveWasLogged(record, "wrong-password");
    }

    [Fact]
    public async Task WhenLoginSucceeds_CountsASuccessfulLogin()
    {
        var attempts = _metrics.Count("grocerease.auth.attempts");
        GivenLoginSucceeds(ExistingUser());

        await _sut.HandleAsync(new LoginCommand("ada@example.com", "Passw0rd!"));

        Assert.Equal(["login/success"], TestMetrics.Attempts(attempts));
    }

    [Fact]
    public async Task WhenLoginFails_CountsAFailedLoginForBothReasons()
    {
        var attempts = _metrics.Count("grocerease.auth.attempts");
        var user = ExistingUser();
        _users.Setup(r => r.GetUserByEmailAsync(Email.Create("ada@example.com"), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.GetUserByEmailAsync(Email.Create("who@example.com"), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        _passwords.Setup(p => p.Verify("wrong-password", user.PasswordHash)).Returns(false);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => _sut.HandleAsync(new LoginCommand("ada@example.com", "wrong-password")));
        await Assert.ThrowsAsync<InvalidCredentialsException>(() => _sut.HandleAsync(new LoginCommand("who@example.com", "wrong-password")));

        // The metric does not split by reason, so a dashboard cannot be used to tell which emails exist.
        Assert.Equal(["login/failure", "login/failure"], TestMetrics.Attempts(attempts));
    }

    private static string? State(FakeLogRecord record, string key) =>
        record.StructuredState!.SingleOrDefault(pair => pair.Key == key).Value;

    private static void AssertNothingSensitiveWasLogged(FakeLogRecord record, string password = "Passw0rd!")
    {
        var everything = record.Message + string.Join(' ', record.StructuredState!.Select(pair => pair.Value));
        Assert.DoesNotContain(password, everything);
        Assert.DoesNotContain("ada@example.com", everything, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refresh-hash", everything);
    }

    private void GivenLoginSucceeds(User user)
    {
        _users
            .Setup(r => r.GetUserByEmailAsync(Email.Create("ada@example.com"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwords.Setup(p => p.Verify("Passw0rd!", user.PasswordHash)).Returns(true);
        _jwt
            .Setup(j => j.GenerateToken(user.Id, user.FirstName, user.LastName, user.Email.Value))
            .Returns("signed-token");
    }

    private void AssertNothingIssuedOrSaved()
    {
        _refreshTokens.Verify(g => g.Issue(), Times.Never);
        _refreshTokenStore.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static User ExistingUser() => User.Register(
        "Ada",
        "Lovelace",
        Email.Create("ada@example.com"),
        "stored-hash",
        new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc));
}
