using System.Text.Json.Nodes;
using GrocerEase.Api.OpenApi;
using GrocerEase.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace GrocerEase.UnitTests.Api;

public class ErrorResponseExamplesTransformerTests
{
    private readonly ErrorResponseExamplesTransformer _transformer = new();

    [Fact]
    public async Task TransformAsync_Register_SetsConflictBadRequestValidationAndServerExamples()
    {
        var operation = OperationWith("400", "409", "422", "500");

        await _transformer.TransformAsync(operation, ContextFor("auth/register"), CancellationToken.None);

        AssertProblem(operation, "409", ErrorCodes.DuplicateEmail, 409, "Duplicate Email", "A user with this email already exists.");
        AssertProblem(operation, "400", ErrorCodes.DatabaseConstraint, 400, "Database Error", "A database constraint was violated.");
        AssertProblem(operation, "500", ErrorCodes.Unexpected, 500, "Internal Server Error", "An unexpected error occurred.");
        AssertValidation(operation);
    }

    [Fact]
    public async Task TransformAsync_Login_SetsUnauthorizedValidationAndServerExamples()
    {
        var operation = OperationWith("401", "422", "500");

        await _transformer.TransformAsync(operation, ContextFor("auth/login"), CancellationToken.None);

        AssertProblem(operation, "401", ErrorCodes.InvalidCredentials, 401, "Unauthorized", "Email or password is invalid.");
        AssertProblem(operation, "500", ErrorCodes.Unexpected, 500, "Internal Server Error", "An unexpected error occurred.");
        AssertValidation(operation);
    }

    [Fact]
    public async Task TransformAsync_Profile_SetsOnlyTheServerExample()
    {
        var operation = OperationWith("401", "500");

        await _transformer.TransformAsync(operation, ContextFor("profile"), CancellationToken.None);

        AssertProblem(operation, "500", ErrorCodes.Unexpected, 500, "Internal Server Error", "An unexpected error occurred.");
        Assert.Null(Media(operation, "401").Example);
    }

    [Fact]
    public async Task TransformAsync_UnknownPath_LeavesExamplesUnset()
    {
        var operation = OperationWith("500");

        await _transformer.TransformAsync(operation, ContextFor("health"), CancellationToken.None);

        Assert.Null(Media(operation, "500").Example);
    }

    [Fact]
    public async Task TransformAsync_MissingProblemMedia_DoesNotThrow()
    {
        var operation = new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["500"] = new OpenApiResponse
                {
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType(),
                    },
                },
            },
        };

        await _transformer.TransformAsync(operation, ContextFor("profile"), CancellationToken.None);

        var response = Assert.IsType<OpenApiResponse>(operation.Responses!["500"]);
        Assert.Null(response.Content!["application/json"].Example);
    }

    private static OpenApiOperation OperationWith(params string[] statuses)
    {
        var responses = new OpenApiResponses();
        foreach (var status in statuses)
        {
            responses[status] = new OpenApiResponse
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/problem+json"] = new OpenApiMediaType(),
                },
            };
        }

        return new OpenApiOperation { Responses = responses };
    }

    private static OpenApiOperationTransformerContext ContextFor(string path) =>
        new()
        {
            DocumentName = "v1",
            Description = new ApiDescription { RelativePath = path },
            ApplicationServices = new ServiceCollection().BuildServiceProvider(),
        };

    private static OpenApiMediaType Media(OpenApiOperation operation, string status)
    {
        var response = Assert.IsType<OpenApiResponse>(operation.Responses![status]);
        return response.Content!["application/problem+json"];
    }

    private static void AssertProblem(
        OpenApiOperation operation,
        string status,
        string code,
        int statusCode,
        string title,
        string detail)
    {
        var example = Assert.IsType<JsonObject>(Media(operation, status).Example);
        Assert.Equal(code, example["code"]!.GetValue<string>());
        Assert.Equal(statusCode, example["status"]!.GetValue<int>());
        Assert.Equal(title, example["title"]!.GetValue<string>());
        Assert.Equal(detail, example["detail"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(example["traceId"]!.GetValue<string>()));
    }

    private static void AssertValidation(OpenApiOperation operation)
    {
        var example = Assert.IsType<JsonObject>(Media(operation, "422").Example);
        Assert.Equal(ErrorCodes.ValidationFailed, example["code"]!.GetValue<string>());
        Assert.Equal(422, example["status"]!.GetValue<int>());
        Assert.Equal("Validation Failed", example["title"]!.GetValue<string>());
        var errors = Assert.IsType<JsonObject>(example["errors"]);
        var email = Assert.IsType<JsonArray>(errors["Email"]);
        Assert.Equal("Email must not be empty.", email[0]!.GetValue<string>());
    }
}
