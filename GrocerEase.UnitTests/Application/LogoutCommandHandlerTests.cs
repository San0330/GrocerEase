using GrocerEase.Application.Authentication.Commands;
using GrocerEase.Application.Authentication.Handlers;
using GrocerEase.Application.Common.Interfaces.Authentication;
using GrocerEase.Application.Common.Interfaces.Persistence;
using GrocerEase.Domain.Entities;
using GrocerEase.UnitTests.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;

namespace GrocerEase.UnitTests.Application;

public sealed class LogoutCommandHandlerTests : IDisposable
{
    private static readonly DateTime Issued = new(2026, 11, 5, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = Issued.AddDays(1);

    private readonly Mock<IRefreshTokenGenerator> _generator = new();
    private readonly Mock<IRefreshTokenRepository> _tokens = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeLogger<LogoutCommandHandler> _log = new();
    private readonly TestMetrics _metrics = new();
    private readonly LogoutCommandHandler _sut;

    public LogoutCommandHandlerTests()
    {
        _generator.Setup(g => g.Hash("presented")).Returns("presented-hash");
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _sut = new LogoutCommandHandler(_generator.Object, _tokens.Object, _unitOfWork.Object, new FixedDateTimeProvider(Now), _log, _metrics.Metrics);
    }

    public void Dispose() => _metrics.Dispose();

    [Fact]
    public async Task WithAKnownToken_RevokesEveryTokenInItsFamilyAndSaves()
    {
        var first = RefreshToken.StartFamily(Guid.NewGuid(), "presented-hash", Issued, Issued.AddDays(30));
        var second = first.Rotate("current-hash", Issued.AddHours(1), Issued.AddDays(30));
        _tokens.Setup(r => r.GetByHashAsync("presented-hash", It.IsAny<CancellationToken>())).ReturnsAsync(second);
        _tokens.Setup(r => r.GetFamilyAsync(first.FamilyId, It.IsAny<CancellationToken>())).ReturnsAsync([first, second]);

        await _sut.HandleAsync(new LogoutCommand("presented"));

        Assert.True(second.IsRevoked);
        Assert.Equal(Now, second.RevokedAt);
        // Already revoked by the rotation, so it keeps that time.
        Assert.Equal(Issued.AddHours(1), first.RevokedAt);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WithAnUnknownToken_SucceedsQuietlyAndSavesNothing()
    {
        _tokens.Setup(r => r.GetByHashAsync("presented-hash", It.IsAny<CancellationToken>())).ReturnsAsync((RefreshToken?)null);

        await _sut.HandleAsync(new LogoutCommand("presented"));

        _tokens.Verify(r => r.GetFamilyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WithAKnownToken_LogsWhoLoggedOutAndWhichSession()
    {
        var userId = Guid.NewGuid();
        var token = RefreshToken.StartFamily(userId, "presented-hash", Issued, Issued.AddDays(30));
        _tokens.Setup(r => r.GetByHashAsync("presented-hash", It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _tokens.Setup(r => r.GetFamilyAsync(token.FamilyId, It.IsAny<CancellationToken>())).ReturnsAsync([token]);

        await _sut.HandleAsync(new LogoutCommand("presented"));

        var record = Assert.Single(_log.Collector.GetSnapshot());
        Assert.Equal(1008, record.Id.Id);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(userId.ToString(), record.StructuredState!.Single(p => p.Key == "UserId").Value);
        Assert.Equal(token.FamilyId.ToString(), record.StructuredState!.Single(p => p.Key == "FamilyId").Value);
        Assert.DoesNotContain("presented", record.Message);
    }

    [Fact]
    public async Task WithAnUnknownToken_LogsNothing_SoTheLogDoesNotRevealWhichTokensExist()
    {
        _tokens.Setup(r => r.GetByHashAsync("presented-hash", It.IsAny<CancellationToken>())).ReturnsAsync((RefreshToken?)null);

        await _sut.HandleAsync(new LogoutCommand("presented"));

        Assert.Empty(_log.Collector.GetSnapshot());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CountsALogout_WhetherOrNotTheTokenWasKnown(bool known)
    {
        var attempts = _metrics.Count("grocerease.auth.attempts");
        var token = RefreshToken.StartFamily(Guid.NewGuid(), "presented-hash", Issued, Issued.AddDays(30));
        _tokens.Setup(r => r.GetByHashAsync("presented-hash", It.IsAny<CancellationToken>())).ReturnsAsync(known ? token : null);
        _tokens.Setup(r => r.GetFamilyAsync(token.FamilyId, It.IsAny<CancellationToken>())).ReturnsAsync([token]);

        await _sut.HandleAsync(new LogoutCommand("presented"));

        // Both answer 204, so both count the same: a metric must not become a way to tell which tokens exist.
        Assert.Equal(["logout/success"], TestMetrics.Attempts(attempts));
    }

    [Fact]
    public async Task ForwardsCancellationTokenToEveryCall()
    {
        using var cts = new CancellationTokenSource();
        var token = RefreshToken.StartFamily(Guid.NewGuid(), "presented-hash", Issued, Issued.AddDays(30));
        _tokens.Setup(r => r.GetByHashAsync("presented-hash", cts.Token)).ReturnsAsync(token);
        _tokens.Setup(r => r.GetFamilyAsync(token.FamilyId, cts.Token)).ReturnsAsync([token]);

        await _sut.HandleAsync(new LogoutCommand("presented"), cts.Token);

        _tokens.Verify(r => r.GetFamilyAsync(token.FamilyId, cts.Token), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(cts.Token), Times.Once);
    }
}
