using GrocerEase.Application.Common.Exceptions;
using GrocerEase.Application.Common.Interfaces.Persistence;
using GrocerEase.Domain.Entities;
using GrocerEase.Domain.ValueObjects;
using GrocerEase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrocerEase.IntegrationTests;

/// <summary>What the database enforces for refresh tokens, and that the repositories read back what they wrote.</summary>
[Collection(IntegrationCollection.Name)]
public sealed class RefreshTokenSchemaTests(GrocerEaseApiFactory factory) : IntegrationTest(factory)
{
    private static readonly DateTime Now = DateTime.UtcNow;

    [Fact]
    public async Task AToken_IsFoundByItsHash_AndItsFamilyListsEveryMember()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var user = await AddUserAsync(scope);
        var hash = UniqueHash();
        var first = RefreshToken.StartFamily(user.Id, hash, Now, Now.AddDays(30));
        var second = first.Rotate(UniqueHash(), Now, Now.AddDays(30));
        var repository = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
        await repository.AddAsync(first);
        await repository.AddAsync(second);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        await using var readScope = Factory.Services.CreateAsyncScope();
        var reader = readScope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
        var found = await reader.GetByHashAsync(hash);
        var family = await reader.GetFamilyAsync(first.FamilyId);

        Assert.NotNull(found);
        Assert.Equal(first.Id, found.Id);
        Assert.True(found.IsRevoked);
        Assert.Equal(2, family.Count);
        Assert.Null(await reader.GetByHashAsync("no-such-hash"));
    }

    [Fact]
    public async Task TwoTokensWithTheSameHash_AreRejected()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var user = await AddUserAsync(scope);
        var hash = UniqueHash();
        var repository = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
        await repository.AddAsync(RefreshToken.StartFamily(user.Id, hash, Now, Now.AddDays(30)));
        await repository.AddAsync(RefreshToken.StartFamily(user.Id, hash, Now, Now.AddDays(30)));

        await Assert.ThrowsAsync<DatabaseConstraintException>(() =>
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync());
    }

    [Fact]
    public async Task ATokenForAnUnknownUser_IsRejected()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
        await repository.AddAsync(RefreshToken.StartFamily(Guid.NewGuid(), UniqueHash(), Now, Now.AddDays(30)));

        await Assert.ThrowsAsync<DatabaseConstraintException>(() =>
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync());
    }

    [Fact]
    public async Task DeletingAUser_DeletesTheirTokens()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var user = await AddUserAsync(scope);
        var hash = UniqueHash();
        await scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>()
            .AddAsync(RefreshToken.StartFamily(user.Id, hash, Now, Now.AddDays(30)));
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await database.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();

        Assert.False(await database.RefreshTokens.AnyAsync(t => t.TokenHash == hash));
    }

    [Fact]
    public async Task TwoRequestsThatRotateTheSameToken_CannotBothSucceed()
    {
        await using var setup = Factory.Services.CreateAsyncScope();
        var user = await AddUserAsync(setup);
        var hash = UniqueHash();
        await setup.ServiceProvider.GetRequiredService<IRefreshTokenRepository>()
            .AddAsync(RefreshToken.StartFamily(user.Id, hash, Now, Now.AddDays(30)));
        await setup.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        await using var firstScope = Factory.Services.CreateAsyncScope();
        await using var secondScope = Factory.Services.CreateAsyncScope();
        var firstToken = await firstScope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>().GetByHashAsync(hash);
        var secondToken = await secondScope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>().GetByHashAsync(hash);
        firstToken!.Revoke(Now);
        secondToken!.Revoke(Now);

        await firstScope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            secondScope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync());
    }

    private static string UniqueHash() => Guid.NewGuid().ToString("N");

    private async Task<User> AddUserAsync(IServiceScope scope)
    {
        var user = User.Register("Ada", "Lovelace", Email.Create(UniqueEmail()), "hash", Now);
        await scope.ServiceProvider.GetRequiredService<IUserRepository>().AddAsync(user);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        return user;
    }
}
