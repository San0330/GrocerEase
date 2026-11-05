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

public sealed class RefreshTokenCommandHandlerTests : IDisposable
{
    private static readonly DateTime Issued = new(2026, 11, 5, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = Issued.AddDays(1);
    private static readonly IssuedRefreshToken Next = new("next-raw", "next-hash", Now.AddDays(30));

    private readonly Mock<IJwtTokenGenerator> _jwt = new();
    private readonly Mock<IRefreshTokenGenerator> _generator = new();
    private readonly Mock<IRefreshTokenRepository> _tokens = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeLogger<RefreshTokenCommandHandler> _log = new();
    private readonly TestMetrics _metrics = new();
    private readonly User _user = User.Register("Ada", "Lovelace", Email.Create("ada@example.com"), "stored-hash", Issued);
    private readonly RefreshTokenCommandHandler _sut;

    public RefreshTokenCommandHandlerTests()
    {
        _generator.Setup(g => g.Hash("presented")).Returns("presented-hash");
        _generator.Setup(g => g.Issue()).Returns(Next);
        _jwt.Setup(j => j.GenerateToken(_user.Id, "Ada", "Lovelace", "ada@example.com")).Returns("new-jwt");
        _users.Setup(u => u.GetByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_user);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _sut = new RefreshTokenCommandHandler(
            _jwt.Object,
            _generator.Object,
            _tokens.Object,
            _users.Object,
            _unitOfWork.Object,
            new FixedDateTimeProvider(Now),
            _log,
            _metrics.Metrics);
    }

    public void Dispose() => _metrics.Dispose();

    [Fact]
    public async Task WithAnActiveToken_ReturnsANewPairAndRotatesTheOldTokenOut()
    {
        var presented = GivenStoredToken();
        RefreshToken? added = null;
        _tokens
            .Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshToken, CancellationToken>((token, _) => added = token)
            .Returns(Task.CompletedTask);

        var result = await _sut.HandleAsync(new RefreshTokenCommand("presented"));

        Assert.Equal(_user.Id, result.Id);
        Assert.Equal("ada@example.com", result.Email);
        Assert.Equal("new-jwt", result.Token);
        Assert.Equal("next-raw", result.RefreshToken);
        Assert.True(presented.IsRevoked);
        Assert.Equal(Now, presented.RevokedAt);
        Assert.NotNull(added);
        Assert.Equal("next-hash", added.TokenHash);
        Assert.Equal(presented.FamilyId, added.FamilyId);
        Assert.Equal(Now.AddDays(30), added.ExpiresAt);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForwardsCancellationTokenToEveryCall()
    {
        using var cts = new CancellationTokenSource();
        GivenStoredToken();

        await _sut.HandleAsync(new RefreshTokenCommand("presented"), cts.Token);

        _tokens.Verify(r => r.GetByHashAsync("presented-hash", cts.Token), Times.Once);
        _users.Verify(u => u.GetByIdAsync(_user.Id, cts.Token), Times.Once);
        _tokens.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), cts.Token), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(cts.Token), Times.Once);
    }

    [Fact]
    public async Task WhenTheTokenIsUnknown_Rejects()
    {
        _tokens.Setup(r => r.GetByHashAsync("presented-hash", It.IsAny<CancellationToken>())).ReturnsAsync((RefreshToken?)null);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _sut.HandleAsync(new RefreshTokenCommand("presented")));

        AssertNothingIssuedOrSaved();
    }

    [Fact]
    public async Task WhenTheTokenHasExpired_RejectsWithoutTouchingTheFamily()
    {
        var stored = RefreshToken.StartFamily(_user.Id, "presented-hash", Issued, Issued.AddHours(12));
        _tokens.Setup(r => r.GetByHashAsync("presented-hash", It.IsAny<CancellationToken>())).ReturnsAsync(stored);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _sut.HandleAsync(new RefreshTokenCommand("presented")));

        Assert.False(stored.IsRevoked);
        AssertNothingIssuedOrSaved();
    }

    [Fact]
    public async Task WhenTheUserNoLongerExists_Rejects()
    {
        GivenStoredToken();
        _users.Setup(u => u.GetByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _sut.HandleAsync(new RefreshTokenCommand("presented")));

        AssertNothingIssuedOrSaved();
    }

    [Fact]
    public async Task WhenARevokedTokenIsPresentedAgain_RevokesTheWholeFamilyAndSavesBeforeRejecting()
    {
        var old = RefreshToken.StartFamily(_user.Id, "presented-hash", Issued, Issued.AddDays(30));
        var current = old.Rotate("current-hash", Issued.AddHours(1), Issued.AddDays(30));
        _tokens.Setup(r => r.GetByHashAsync("presented-hash", It.IsAny<CancellationToken>())).ReturnsAsync(old);
        _tokens.Setup(r => r.GetFamilyAsync(old.FamilyId, It.IsAny<CancellationToken>())).ReturnsAsync([old, current]);
        var saved = false;
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => saved = true)
            .Returns(Task.CompletedTask);

        var thrown = await Record.ExceptionAsync(() => _sut.HandleAsync(new RefreshTokenCommand("presented")));

        Assert.IsType<InvalidRefreshTokenException>(thrown);
        // The save has happened by the time the exception leaves, or the revocation would be rolled back with it.
        Assert.True(saved);
        Assert.True(current.IsRevoked);
        Assert.Equal(Now, current.RevokedAt);
        // The token that was already revoked keeps its first time.
        Assert.Equal(Issued.AddHours(1), old.RevokedAt);
        _generator.Verify(g => g.Issue(), Times.Never);
        _tokens.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WhenReuseIsDetected_TheRevocationIgnoresAClientThatHasGoneAway()
    {
        using var cts = new CancellationTokenSource();
        var old = RefreshToken.StartFamily(_user.Id, "presented-hash", Issued, Issued.AddDays(30));
        old.Revoke(Issued.AddHours(1));
        _tokens.Setup(r => r.GetByHashAsync("presented-hash", It.IsAny<CancellationToken>())).ReturnsAsync(old);
        _tokens.Setup(r => r.GetFamilyAsync(old.FamilyId, It.IsAny<CancellationToken>())).ReturnsAsync([old]);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _sut.HandleAsync(new RefreshTokenCommand("presented"), cts.Token));

        // Cancelling must not be a way to stop a stolen family from being revoked.
        _tokens.Verify(r => r.GetFamilyAsync(old.FamilyId, CancellationToken.None), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task WithAnActiveToken_LogsTheRefreshWithTheUserAndFamily_AfterTheSave()
    {
        var presented = GivenStoredToken();

        await _sut.HandleAsync(new RefreshTokenCommand("presented"));

        var record = Assert.Single(_log.Collector.GetSnapshot());
        Assert.Equal(1005, record.Id.Id);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(_user.Id.ToString(), State(record, "UserId"));
        Assert.Equal(presented.FamilyId.ToString(), State(record, "FamilyId"));
    }

    [Fact]
    public async Task WhenTheTokenIsUnknown_LogsTheReason()
    {
        _tokens.Setup(r => r.GetByHashAsync("presented-hash", It.IsAny<CancellationToken>())).ReturnsAsync((RefreshToken?)null);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _sut.HandleAsync(new RefreshTokenCommand("presented")));

        AssertRejectedFor("unknown_token");
    }

    [Fact]
    public async Task WhenTheTokenHasExpired_LogsTheReason()
    {
        var stored = RefreshToken.StartFamily(_user.Id, "presented-hash", Issued, Issued.AddHours(12));
        _tokens.Setup(r => r.GetByHashAsync("presented-hash", It.IsAny<CancellationToken>())).ReturnsAsync(stored);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _sut.HandleAsync(new RefreshTokenCommand("presented")));

        AssertRejectedFor("expired_token");
    }

    [Fact]
    public async Task WhenTheUserNoLongerExists_LogsTheReason()
    {
        GivenStoredToken();
        _users.Setup(u => u.GetByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _sut.HandleAsync(new RefreshTokenCommand("presented")));

        AssertRejectedFor("user_missing");
    }

    [Fact]
    public async Task WhenARevokedTokenIsPresentedAgain_LogsAWarningWithHowManyTokensWereRevoked()
    {
        var old = RefreshToken.StartFamily(_user.Id, "presented-hash", Issued, Issued.AddDays(30));
        var current = old.Rotate("current-hash", Issued.AddHours(1), Issued.AddDays(30));
        _tokens.Setup(r => r.GetByHashAsync("presented-hash", It.IsAny<CancellationToken>())).ReturnsAsync(old);
        _tokens.Setup(r => r.GetFamilyAsync(old.FamilyId, It.IsAny<CancellationToken>())).ReturnsAsync([old, current]);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _sut.HandleAsync(new RefreshTokenCommand("presented")));

        var record = Assert.Single(_log.Collector.GetSnapshot());
        Assert.Equal(1007, record.Id.Id);
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal(_user.Id.ToString(), State(record, "UserId"));
        Assert.Equal(old.FamilyId.ToString(), State(record, "FamilyId"));
        // Only the token that was still live counts: the one the rotation already revoked was dead before the reuse.
        Assert.Equal("1", State(record, "TokensRevoked"));
    }

    [Fact]
    public async Task NoRefreshLogLineContainsTheTokenOrItsHash()
    {
        var old = RefreshToken.StartFamily(_user.Id, "presented-hash", Issued, Issued.AddDays(30));
        old.Revoke(Issued.AddHours(1));
        _tokens.Setup(r => r.GetByHashAsync("presented-hash", It.IsAny<CancellationToken>())).ReturnsAsync(old);
        _tokens.Setup(r => r.GetFamilyAsync(old.FamilyId, It.IsAny<CancellationToken>())).ReturnsAsync([old]);
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _sut.HandleAsync(new RefreshTokenCommand("presented")));
        GivenStoredToken();
        await _sut.HandleAsync(new RefreshTokenCommand("presented"));

        foreach (var record in _log.Collector.GetSnapshot())
        {
            var everything = record.Message + string.Join(' ', record.StructuredState!.Select(pair => pair.Value));
            Assert.DoesNotContain("presented", everything);
            Assert.DoesNotContain("next-raw", everything);
            Assert.DoesNotContain("next-hash", everything);
            Assert.DoesNotContain("new-jwt", everything);
        }
    }

    [Fact]
    public async Task WithAnActiveToken_CountsASuccessfulRefresh()
    {
        var attempts = _metrics.Count("grocerease.auth.attempts");
        var reuse = _metrics.Count("grocerease.auth.refresh_token_reuse");
        GivenStoredToken();

        await _sut.HandleAsync(new RefreshTokenCommand("presented"));

        Assert.Equal(["refresh/success"], TestMetrics.Attempts(attempts));
        Assert.Empty(reuse.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task WhenARefreshIsRejected_CountsAFailureWhateverTheReason()
    {
        var attempts = _metrics.Count("grocerease.auth.attempts");
        var reuse = _metrics.Count("grocerease.auth.refresh_token_reuse");
        _tokens.Setup(r => r.GetByHashAsync("presented-hash", It.IsAny<CancellationToken>())).ReturnsAsync((RefreshToken?)null);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _sut.HandleAsync(new RefreshTokenCommand("presented")));

        Assert.Equal(["refresh/failure"], TestMetrics.Attempts(attempts));
        Assert.Empty(reuse.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task WhenAnExpiredTokenOrAMissingUserIsRejected_BothCountAsFailures()
    {
        var attempts = _metrics.Count("grocerease.auth.attempts");
        var expired = RefreshToken.StartFamily(_user.Id, "presented-hash", Issued, Issued.AddHours(12));
        _tokens.Setup(r => r.GetByHashAsync("presented-hash", It.IsAny<CancellationToken>())).ReturnsAsync(expired);
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _sut.HandleAsync(new RefreshTokenCommand("presented")));

        GivenStoredToken();
        _users.Setup(u => u.GetByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _sut.HandleAsync(new RefreshTokenCommand("presented")));

        Assert.Equal(["refresh/failure", "refresh/failure"], TestMetrics.Attempts(attempts));
    }

    [Fact]
    public async Task WhenARevokedTokenIsPresentedAgain_CountsTheReuseAndAFailedRefresh()
    {
        var attempts = _metrics.Count("grocerease.auth.attempts");
        var reuse = _metrics.Count("grocerease.auth.refresh_token_reuse");
        var old = RefreshToken.StartFamily(_user.Id, "presented-hash", Issued, Issued.AddDays(30));
        old.Revoke(Issued.AddHours(1));
        _tokens.Setup(r => r.GetByHashAsync("presented-hash", It.IsAny<CancellationToken>())).ReturnsAsync(old);
        _tokens.Setup(r => r.GetFamilyAsync(old.FamilyId, It.IsAny<CancellationToken>())).ReturnsAsync([old]);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _sut.HandleAsync(new RefreshTokenCommand("presented")));

        Assert.Equal(1, Assert.Single(reuse.GetMeasurementSnapshot()).Value);
        Assert.Equal(["refresh/failure"], TestMetrics.Attempts(attempts));
    }

    private void AssertRejectedFor(string reason)
    {
        var record = Assert.Single(_log.Collector.GetSnapshot());
        Assert.Equal(1006, record.Id.Id);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(reason, State(record, "Reason"));
    }

    private static string? State(FakeLogRecord record, string key) =>
        record.StructuredState!.SingleOrDefault(pair => pair.Key == key).Value;

    private RefreshToken GivenStoredToken()
    {
        var stored = RefreshToken.StartFamily(_user.Id, "presented-hash", Issued, Issued.AddDays(30));
        _tokens.Setup(r => r.GetByHashAsync("presented-hash", It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        return stored;
    }

    private void AssertNothingIssuedOrSaved()
    {
        _generator.Verify(g => g.Issue(), Times.Never);
        _jwt.Verify(j => j.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _tokens.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
