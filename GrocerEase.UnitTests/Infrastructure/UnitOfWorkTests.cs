using GrocerEase.Application.Common.Exceptions;
using GrocerEase.Domain.Exceptions;
using GrocerEase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GrocerEase.UnitTests.Infrastructure;

public class UnitOfWorkTests
{
    [Fact]
    public async Task SaveChangesAsync_WhenTheEmailIndexRejectsTheRow_ThrowsDuplicateEmail()
    {
        var failure = Postgres(PostgresErrorCodes.UniqueViolation, AppDbContext.UserEmailIndex);

        var thrown = await Assert.ThrowsAsync<DuplicateEmailException>(() => SaveFailingWith(failure));

        Assert.Same(failure, thrown.InnerException!.InnerException);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenAnotherUniqueIndexRejectsTheRow_ThrowsDatabaseConstraint()
    {
        var failure = Postgres(PostgresErrorCodes.UniqueViolation, "PK_Users");

        await Assert.ThrowsAsync<DatabaseConstraintException>(() => SaveFailingWith(failure));
    }

    [Theory]
    [InlineData(PostgresErrorCodes.NotNullViolation)]
    [InlineData(PostgresErrorCodes.ForeignKeyViolation)]
    [InlineData(PostgresErrorCodes.CheckViolation)]
    public async Task SaveChangesAsync_WhenAnyOtherIntegrityRuleFails_ThrowsDatabaseConstraint(string sqlState)
    {
        await Assert.ThrowsAsync<DatabaseConstraintException>(() => SaveFailingWith(Postgres(sqlState)));
    }

    [Theory]
    [InlineData(PostgresErrorCodes.DeadlockDetected)]
    [InlineData(PostgresErrorCodes.QueryCanceled)]
    public async Task SaveChangesAsync_WhenTheDatabaseFailsForAnotherReason_RethrowsTheOriginal(string sqlState)
    {
        await Assert.ThrowsAsync<DbUpdateException>(() => SaveFailingWith(Postgres(sqlState)));
    }

    [Fact]
    public async Task SaveChangesAsync_WhenTheFailureIsNotFromPostgres_RethrowsTheOriginal()
    {
        await Assert.ThrowsAsync<DbUpdateException>(() => SaveFailingWith(new TimeoutException()));
    }

    private static Task SaveFailingWith(Exception inner) =>
        new UnitOfWork(new FailingContext(new DbUpdateException("update failed", inner))).SaveChangesAsync();

    private static PostgresException Postgres(string sqlState, string? constraintName = null) =>
        new("failed", "ERROR", "ERROR", sqlState, constraintName: constraintName);

    private sealed class FailingContext(DbUpdateException failure) : AppDbContext(
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=unused").Options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<int>(failure);
    }
}
