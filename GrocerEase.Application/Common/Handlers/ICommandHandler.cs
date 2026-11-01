namespace GrocerEase.Application.Common.Handlers;

/// <summary>One use case: takes a command that has already been validated and carries it out.</summary>
public interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
