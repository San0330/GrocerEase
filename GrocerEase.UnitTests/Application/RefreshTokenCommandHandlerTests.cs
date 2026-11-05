using GrocerEase.Application.Authentication.Commands;
using GrocerEase.Application.Authentication.Handlers;
using GrocerEase.Application.Common.Interfaces.Authentication;
using GrocerEase.Application.Common.Interfaces.Persistence;
using GrocerEase.Domain.Entities;
using GrocerEase.Domain.Exceptions;
using GrocerEase.Domain.ValueObjects;
using GrocerEase.UnitTests.Support;
using Moq;

namespace GrocerEase.UnitTests.Application;

public class RefreshTokenCommandHandlerTests
{
    private static readonly DateTime Issued = new(2026, 11, 5, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = Issued.AddDays(1);
    private static readonly IssuedRefreshToken Next = new("next-raw", "next-hash", Now.AddDays(30));

    private readonly Mock<IJwtTokenGenerator> _jwt = new();
    private readonly Mock<IRefreshTokenGenerator> _generator = new();
    private readonly Mock<IRefreshTokenRepository> _tokens = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
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
            new FixedDateTimeProvider(Now));
    }

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
