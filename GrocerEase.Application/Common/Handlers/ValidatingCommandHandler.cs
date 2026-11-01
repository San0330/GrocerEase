using FluentValidation;

namespace GrocerEase.Application.Common.Handlers;

/// <summary>
/// Validates a command before its handler sees it, so no handler repeats the call and none can forget it.
/// A failure throws <see cref="ValidationException"/> and the handler never runs.
/// </summary>
public sealed class ValidatingCommandHandler<TCommand, TResult> : ICommandHandler<TCommand, TResult>
{
    private readonly ICommandHandler<TCommand, TResult> _inner;
    private readonly IValidator<TCommand> _validator;

    public ValidatingCommandHandler(ICommandHandler<TCommand, TResult> inner, IValidator<TCommand> validator)
    {
        _inner = inner;
        _validator = validator;
    }

    public async Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        return await _inner.HandleAsync(command, cancellationToken);
    }
}
