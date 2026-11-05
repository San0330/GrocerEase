namespace GrocerEase.Application.Common.Handlers;

/// <summary>The result of a command that has nothing to return, so it can still go through <see cref="ICommandHandler{TCommand,TResult}"/>.</summary>
public readonly struct Unit
{
    public static Unit Value => default;
}
