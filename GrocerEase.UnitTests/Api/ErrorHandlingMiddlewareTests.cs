using System.Diagnostics;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using GrocerEase.Api.MiddleWare;
using GrocerEase.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrocerEase.UnitTests.Api;

public class ErrorHandlingMiddlewareTests
{
    private static readonly IServiceProvider JsonServices = new ServiceCollection()
        .AddLogging()
        .Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(_ => { })
        .BuildServiceProvider();

    [Fact]
    public async Task InvokeAsync_WhenTheNextDelegateSucceeds_LeavesTheResponseUntouched()
    {
        var result = await InvokeAsync(_ => Task.CompletedTask);

        Assert.Equal(StatusCodes.Status200OK, result.Status);
        Assert.Null(result.Body);
    }

    [Fact]
    public async Task InvokeAsync_ValidationException_ReturnsGroupedFieldErrors()
    {
        var exception = new ValidationException(
        [
            new ValidationFailure("Email", "Email is required."),
            new ValidationFailure("Email", "Email is not valid."),
            new ValidationFailure("Password", "Password is required."),
        ]);

        using var activity = new Activity("request");
        activity.Start();

        var result = await InvokeAsync(_ => throw exception, traceIdentifier: "http-trace");

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.Status);
        Assert.StartsWith("application/problem+json", result.ContentType);
        var body = result.Body!.RootElement;
        Assert.Equal(ErrorCodes.ValidationFailed, body.GetProperty("code").GetString());
        Assert.Equal(422, body.GetProperty("status").GetInt32());
        Assert.Equal("Validation Failed", body.GetProperty("title").GetString());
        Assert.Equal(activity.Id, body.GetProperty("traceId").GetString());

        var errors = body.GetProperty("errors");
        Assert.Equal(
            new[] { "Email is required.", "Email is not valid." },
            errors.GetProperty("Email").EnumerateArray().Select(item => item.GetString()).ToArray());
        Assert.Equal(
            "Password is required.",
            Assert.Single(errors.GetProperty("Password").EnumerateArray()).GetString());
    }

    [Fact]
    public async Task InvokeAsync_InvalidCredentials_ReturnsUnauthorized()
    {
        var result = await InvokeAsync(
            _ => throw new InvalidCredentialsException(),
            traceIdentifier: "http-trace");

        Assert.Equal(StatusCodes.Status401Unauthorized, result.Status);
        var body = result.Body!.RootElement;
        Assert.Equal(ErrorCodes.InvalidCredentials, body.GetProperty("code").GetString());
        Assert.Equal(401, body.GetProperty("status").GetInt32());
        Assert.Equal("Unauthorized", body.GetProperty("title").GetString());
        Assert.Equal("Email or password is invalid.", body.GetProperty("detail").GetString());
        Assert.Equal("http-trace", body.GetProperty("traceId").GetString());
    }

    [Theory]
    [InlineData(400, "Bad Request")]
    [InlineData(403, "Forbidden")]
    [InlineData(404, "Not Found")]
    [InlineData(409, "Conflict")]
    [InlineData(422, "Unprocessable Entity")]
    [InlineData(418, "Error")]
    public async Task InvokeAsync_AppException_UsesTheStatusReasonPhrase(int statusCode, string title)
    {
        var result = await InvokeAsync(_ => throw new StubAppException(statusCode));

        Assert.Equal(statusCode, result.Status);
        var body = result.Body!.RootElement;
        Assert.Equal("Test.Code", body.GetProperty("code").GetString());
        Assert.Equal("detail text", body.GetProperty("detail").GetString());
        Assert.Equal(title, body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task InvokeAsync_DuplicateEmailException_ReturnsDuplicateEmail()
    {
        var result = await InvokeAsync(_ => throw new DuplicateEmailException());

        Assert.Equal(StatusCodes.Status409Conflict, result.Status);
        var body = result.Body!.RootElement;
        Assert.Equal(ErrorCodes.DuplicateEmail, body.GetProperty("code").GetString());
        Assert.Equal("Duplicate Email", body.GetProperty("title").GetString());
        Assert.Equal("A user with this email already exists.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_UniqueViolation_ReturnsDuplicateEmail()
    {
        var exception = new DbUpdateException("update failed", new Exception("duplicate key value violates unique constraint 23505"));

        var result = await InvokeAsync(_ => throw exception);

        Assert.Equal(StatusCodes.Status409Conflict, result.Status);
        var body = result.Body!.RootElement;
        Assert.Equal(ErrorCodes.DuplicateEmail, body.GetProperty("code").GetString());
        Assert.Equal("Duplicate Email", body.GetProperty("title").GetString());
        Assert.Equal("A user with this email already exists.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_OtherDatabaseFailure_ReturnsAConstraintProblem()
    {
        var exception = new DbUpdateException("23505 is only on the outer message", new Exception("check constraint"));

        var result = await InvokeAsync(_ => throw exception);

        Assert.Equal(StatusCodes.Status400BadRequest, result.Status);
        var body = result.Body!.RootElement;
        Assert.Equal(ErrorCodes.DatabaseConstraint, body.GetProperty("code").GetString());
        Assert.Equal("Database Error", body.GetProperty("title").GetString());
        Assert.Equal("A database constraint was violated.", body.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData(400, "Bad Request")]
    [InlineData(408, "Request Timeout")]
    [InlineData(413, "Content Too Large")]
    [InlineData(415, "Unsupported Media Type")]
    public async Task InvokeAsync_BadHttpRequest_KeepsKestrelsStatusInsteadOfReturning500(int statusCode, string title)
    {
        var result = await InvokeAsync(
            _ => throw new BadHttpRequestException("Request body too large. Limit is 30000000 bytes.", statusCode),
            traceIdentifier: "http-trace");

        Assert.Equal(statusCode, result.Status);
        Assert.StartsWith("application/problem+json", result.ContentType);
        var body = result.Body!.RootElement;
        Assert.Equal(ErrorCodes.MalformedRequest, body.GetProperty("code").GetString());
        Assert.Equal(statusCode, body.GetProperty("status").GetInt32());
        Assert.Equal(title, body.GetProperty("title").GetString());
        Assert.Equal("The request could not be read.", body.GetProperty("detail").GetString());
        Assert.Equal("http-trace", body.GetProperty("traceId").GetString());
        Assert.DoesNotContain("30000000", body.GetRawText());
    }

    [Fact]
    public async Task InvokeAsync_UnexpectedException_HidesTheExceptionMessage()
    {
        var result = await InvokeAsync(_ => throw new InvalidOperationException("secret connection string"));

        Assert.Equal(StatusCodes.Status500InternalServerError, result.Status);
        var body = result.Body!.RootElement;
        Assert.Equal(ErrorCodes.Unexpected, body.GetProperty("code").GetString());
        Assert.Equal("Internal Server Error", body.GetProperty("title").GetString());
        Assert.Equal("An unexpected error occurred.", body.GetProperty("detail").GetString());
        Assert.DoesNotContain("secret", body.GetRawText());
    }

    [Fact]
    public async Task InvokeAsync_WhenTheClientAbortsTheRequest_DoesNotWriteAnErrorBody()
    {
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();

        var result = await InvokeAsync(
            _ => throw new OperationCanceledException(aborted.Token),
            requestAborted: aborted.Token);

        Assert.Equal(499, result.Status);
        Assert.Null(result.Body);
    }

    [Fact]
    public async Task InvokeAsync_OperationCanceledWithoutAnAbort_ReturnsInternalServerError()
    {
        var result = await InvokeAsync(_ => throw new OperationCanceledException("upstream timeout"));

        Assert.Equal(StatusCodes.Status500InternalServerError, result.Status);
    }

    private static async Task<ErrorResult> InvokeAsync(
        RequestDelegate next,
        string? traceIdentifier = null,
        CancellationToken requestAborted = default)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = JsonServices,
            TraceIdentifier = traceIdentifier ?? "http-trace",
            RequestAborted = requestAborted,
        };
        context.Response.Body = new MemoryStream();

        var middleware = new ErrorHandlingMiddleware(next, NullLogger<ErrorHandlingMiddleware>.Instance);
        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        JsonDocument? body = context.Response.Body.Length == 0
            ? null
            : await JsonDocument.ParseAsync(context.Response.Body);

        return new ErrorResult(context.Response.StatusCode, context.Response.ContentType, body);
    }

    private sealed record ErrorResult(int Status, string? ContentType, JsonDocument? Body);

    private sealed class StubAppException(int statusCode) : Exception("detail text"), IAppException
    {
        public int StatusCode { get; } = statusCode;
        public string ErrorMessage => "detail text";
        public string ErrorCode => "Test.Code";
    }
}
