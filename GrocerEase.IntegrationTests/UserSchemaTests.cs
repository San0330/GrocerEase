using GrocerEase.Application.Common.Exceptions;
using GrocerEase.Application.Common.Interfaces.Persistence;
using GrocerEase.Domain.Entities;
using GrocerEase.Domain.Exceptions;
using GrocerEase.Domain.ValueObjects;
using GrocerEase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GrocerEase.IntegrationTests;

/// <summary>
/// The rules the database enforces on its own, in case something writes to it without going through the validators
/// or <see cref="Email.Create"/>: a later import, a script, a bug.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class UserSchemaTests(GrocerEaseApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Email_IsUniqueRegardlessOfCase_EvenForARowWrittenWithoutEmailCreate()
    {
        var local = Guid.NewGuid().ToString("N");

        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Users" ("Id", "FirstName", "LastName", "Email", "PasswordHash", "CreatedAt")
            VALUES ({Guid.CreateVersion7()}, 'Ada', 'Lovelace', {local.ToUpperInvariant() + "@Example.com"}, 'hash', {DateTime.UtcNow})
            """);

        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await users.AddAsync(NewUser(Email.Create($"{local}@example.com"), "Ada"));

        await Assert.ThrowsAsync<DuplicateEmailException>(() => unitOfWork.SaveChangesAsync());
    }

    [Fact]
    public async Task Name_LongerThanTheColumn_IsRejectedByTheDatabase()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await users.AddAsync(NewUser(Email.Create(UniqueEmail()), new string('A', User.NameMaxLength + 1)));

        // A value that is too long is not a constraint violation (SQLSTATE class 22, not 23), so it surfaces untranslated.
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => unitOfWork.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.StringDataRightTruncation, Assert.IsType<PostgresException>(failure.InnerException).SqlState);
    }

    [Fact]
    public async Task Email_LongerThanAnAddressCanBe_IsRejectedByTheCheckConstraint()
    {
        var tooLong = new string('a', Email.MaxLength) + "@example.com";

        await using var scope = Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await users.AddAsync(NewUser(Email.Create(tooLong), "Ada"));

        await Assert.ThrowsAsync<DatabaseConstraintException>(() => unitOfWork.SaveChangesAsync());
    }

    [Fact]
    public async Task Email_AtTheLimit_IsAccepted()
    {
        const string domain = "@example.com";
        var atLimit = Guid.NewGuid().ToString("N").PadRight(Email.MaxLength - domain.Length, 'a') + domain;
        Assert.Equal(Email.MaxLength, atLimit.Length);

        await using var scope = Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await users.AddAsync(NewUser(Email.Create(atLimit), "Ada"));

        await unitOfWork.SaveChangesAsync();
    }

    private static User NewUser(Email email, string firstName) =>
        User.Register(firstName, "Lovelace", email, "hash", DateTime.UtcNow);
}
