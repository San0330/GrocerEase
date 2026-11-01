using GrocerEase.Application.Common.Interfaces.Persistence;
using GrocerEase.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GrocerEase.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                           {
                                               SqlState: PostgresErrorCodes.UniqueViolation,
                                               ConstraintName: AppDbContext.UserEmailIndex
                                           })
        {
            throw new DuplicateEmailException(ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: { } sqlState }
                                           && sqlState.StartsWith("23", StringComparison.Ordinal))
        {
            // SQLSTATE class 23 is "integrity constraint violation": the caller's data broke a rule.
            // Every other database failure (connection loss, timeout, deadlock) stays a server fault.
            throw new DatabaseConstraintException(ex);
        }
    }
}
