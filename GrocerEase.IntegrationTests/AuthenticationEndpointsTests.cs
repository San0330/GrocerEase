using System.Net;
using System.Net.Http.Json;
using GrocerEase.Contracts.Authentication;
using GrocerEase.Application.Common.Interfaces.Persistence;
using GrocerEase.Contracts.Errors;
using GrocerEase.Domain.Entities;
using GrocerEase.Domain.Exceptions;
using GrocerEase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrocerEase.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class AuthenticationEndpointsTests(GrocerEaseApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Register_PersistsAHashedPasswordAndLowercasedEmail()
    {
        var email = $"Ada-{Guid.NewGuid():N}@Example.COM";

        var registered = await RegisterAsync(email);

        Assert.Equal(email.ToLowerInvariant(), registered.Email);
        Assert.Equal("Ada", registered.FirstName);
        Assert.Equal("Lovelace", registered.LastName);
        Assert.False(string.IsNullOrWhiteSpace(registered.Token));

        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await database.Users.SingleAsync(u => u.Id == registered.Id);

        Assert.Equal(email.ToLowerInvariant(), user.Email);
        Assert.NotEqual("Passw0rd!", user.PasswordHash);
        Assert.StartsWith("$2", user.PasswordHash);
        Assert.Equal(DateTimeKind.Utc, user.CreatedAt.Kind);
        Assert.InRange(user.CreatedAt, DateTime.UtcNow.AddMinutes(-2), DateTime.UtcNow.AddMinutes(2));
    }

    [Fact]
    public async Task SaveChanges_WhenTwoRequestsRaceToTheSameEmail_ThrowsDuplicateEmail()
    {
        var email = UniqueEmail();

        await using var scope = Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await users.AddAsync(NewUser(email));
        await unitOfWork.SaveChangesAsync();

        // The service's own existence check is skipped here, as it would be by the losing request in a race.
        await users.AddAsync(NewUser(email));
        await Assert.ThrowsAsync<DuplicateEmailException>(() => unitOfWork.SaveChangesAsync());
    }

    private static User NewUser(string email) => new()
    {
        Id = Guid.CreateVersion7(),
        FirstName = "Ada",
        LastName = "Lovelace",
        Email = email,
        PasswordHash = "hash",
    };

    [Fact]
    public async Task Register_WithTheSameEmailInAnotherCase_ReturnsDuplicateEmail()
    {
        var email = $"Ada-{Guid.NewGuid():N}@Example.COM";
        await RegisterAsync(email);

        var response = await Client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("Ada", "Lovelace", email.ToLowerInvariant(), "Passw0rd!"));

        var problem = await ReadAsync<ErrorResponse>(response, HttpStatusCode.Conflict);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(ErrorCodes.DuplicateEmail, problem.Code);
        Assert.Equal(409, problem.Status);
        Assert.Equal("Duplicate Email", problem.Title);
        Assert.Equal("A user with this email already exists.", problem.Detail);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    [Fact]
    public async Task Register_WithAWeakPassword_ReturnsValidationProblem()
    {
        var response = await Client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("Ada", "Lovelace", UniqueEmail(), "password"));

        var problem = await ReadAsync<ValidationErrorResponse>(response, HttpStatusCode.UnprocessableEntity);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(ErrorCodes.ValidationFailed, problem.Code);
        Assert.Equal(422, problem.Status);
        Assert.Equal("Validation Failed", problem.Title);
        Assert.True(problem.Errors.ContainsKey("Password"));
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    [Fact]
    public async Task Login_WithMatchingCredentials_ReturnsATokenForThatUser()
    {
        var email = $"Ada-{Guid.NewGuid():N}@Example.COM";
        var registered = await RegisterAsync(email);

        var response = await Client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(email.ToLowerInvariant(), "Passw0rd!"));

        var loggedIn = await ReadAsync<AuthenticationResponse>(response, HttpStatusCode.OK);
        Assert.Equal(registered.Id, loggedIn.Id);
        Assert.Equal(email.ToLowerInvariant(), loggedIn.Email);
        Assert.False(string.IsNullOrWhiteSpace(loggedIn.Token));
    }

    [Fact]
    public async Task Login_WithTheWrongPassword_ReturnsInvalidCredentials()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);

        var response = await Client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(email, "wrong-password"));

        await AssertInvalidCredentials(response);
    }

    [Fact]
    public async Task Login_WithAnUnknownEmail_ReturnsInvalidCredentials()
    {
        var response = await Client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(UniqueEmail(), "Passw0rd!"));

        await AssertInvalidCredentials(response);
    }

    private async Task AssertInvalidCredentials(HttpResponseMessage response)
    {
        var problem = await ReadAsync<ErrorResponse>(response, HttpStatusCode.Unauthorized);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(ErrorCodes.InvalidCredentials, problem.Code);
        Assert.Equal(401, problem.Status);
        Assert.Equal("Unauthorized", problem.Title);
        Assert.Equal("Email or password is invalid.", problem.Detail);
    }
}
