using FluentValidation;
using GrocerEase.Application.Common.Handlers;
using Moq;

namespace GrocerEase.UnitTests.Application;

public class ValidatingCommandHandlerTests
{
    public sealed record Ping(string Name);

    public sealed class PingValidator : AbstractValidator<Ping>
    {
        public PingValidator() => RuleFor(x => x.Name).NotEmpty();
    }

    private readonly Mock<ICommandHandler<Ping, string>> _inner = new();
    private readonly ValidatingCommandHandler<Ping, string> _sut;

    public ValidatingCommandHandlerTests()
    {
        _sut = new ValidatingCommandHandler<Ping, string>(_inner.Object, new PingValidator());
    }

    [Fact]
    public async Task HandleAsync_WithAValidCommand_ReturnsWhatTheInnerHandlerReturns()
    {
        using var cts = new CancellationTokenSource();
        var command = new Ping("ada");
        _inner.Setup(h => h.HandleAsync(command, cts.Token)).ReturnsAsync("pong");

        var result = await _sut.HandleAsync(command, cts.Token);

        Assert.Equal("pong", result);
    }

    [Fact]
    public async Task HandleAsync_WithAnInvalidCommand_ThrowsAndNeverReachesTheInnerHandler()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() => _sut.HandleAsync(new Ping("")));

        Assert.Equal("Name", Assert.Single(exception.Errors).PropertyName);
        _inner.Verify(h => h.HandleAsync(It.IsAny<Ping>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
