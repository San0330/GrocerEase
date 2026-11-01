using System.Text.Json.Nodes;
using GrocerEase.Contracts.Errors;
using GrocerEase.Domain.Exceptions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace GrocerEase.Api.OpenApi;

public sealed class ErrorResponseExamplesTransformer : IOpenApiOperationTransformer
{
    private const string TraceId = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var path = context.Description.RelativePath;

        if (path == "auth/register")
        {
            Set(operation, "409", ErrorCodes.DuplicateEmail, 409, "Duplicate Email", "A user with this email already exists.");
            SetRegisterBadRequest(operation);
        }

        if (path == "auth/login")
        {
            Set(operation, "401", ErrorCodes.InvalidCredentials, 401, "Unauthorized", "Email or password is invalid.");
            SetMalformed(operation);
        }

        if (path is "auth/register" or "auth/login")
        {
            SetValidation(operation);
        }

        if (path is "auth/register" or "auth/login")
        {
            Set(operation, "429", ErrorCodes.RateLimited, 429, "Too Many Requests", "Too many requests. Try again later.");
        }

        if (path is "auth/register" or "auth/login" or "profile")
        {
            Set(operation, "500", ErrorCodes.Unexpected, 500, "Internal Server Error", "An unexpected error occurred.");
        }

        return Task.CompletedTask;
    }

    private static void Set(OpenApiOperation operation, string status, string code, int statusCode, string title, string detail)
    {
        if (!TryGetProblemMedia(operation, status, out var media))
            return;

        media.Example = new JsonObject
        {
            ["code"] = code,
            ["status"] = statusCode,
            ["title"] = title,
            ["detail"] = detail,
            ["traceId"] = TraceId,
        };
    }

    // A malformed body is rejected by MVC model binding before the controller runs (ValidationErrorResponse).
    private static void SetMalformed(OpenApiOperation operation)
    {
        if (!TryGetProblemMedia(operation, "400", out var media))
            return;

        media.Example = MalformedExample();
    }

    // Register can answer 400 in two shapes: the malformed body above, or a database constraint failure
    // (ErrorResponse). One status code cannot carry two types through [ProducesResponseType], so publish both.
    private static void SetRegisterBadRequest(OpenApiOperation operation)
    {
        if (!TryGetProblemMedia(operation, "400", out var media))
            return;

        media.Schema = new OpenApiSchema
        {
            OneOf =
            [
                new OpenApiSchemaReference(nameof(ValidationErrorResponse)),
                new OpenApiSchemaReference(nameof(ErrorResponse)),
            ],
        };
        media.Example = null;
        media.Examples = new Dictionary<string, IOpenApiExample>
        {
            ["malformedRequest"] = new OpenApiExample
            {
                Summary = "The body could not be read",
                Value = MalformedExample(),
            },
            ["databaseConstraint"] = new OpenApiExample
            {
                Summary = "A database constraint was violated",
                Value = new JsonObject
                {
                    ["code"] = ErrorCodes.DatabaseConstraint,
                    ["status"] = 400,
                    ["title"] = "Database Error",
                    ["detail"] = "A database constraint was violated.",
                    ["traceId"] = TraceId,
                },
            },
        };
    }

    private static JsonObject MalformedExample() => new()
    {
        ["code"] = ErrorCodes.MalformedRequest,
        ["status"] = StatusCodes.Status400BadRequest,
        ["title"] = "Malformed Request",
        ["errors"] = new JsonObject
        {
            ["Password"] = new JsonArray("The Password field is required."),
        },
        ["traceId"] = TraceId,
    };

    private static void SetValidation(OpenApiOperation operation)
    {
        if (!TryGetProblemMedia(operation, "422", out var media))
            return;

        media.Example = new JsonObject
        {
            ["code"] = ErrorCodes.ValidationFailed,
            ["status"] = StatusCodes.Status422UnprocessableEntity,
            ["title"] = "Validation Failed",
            ["errors"] = new JsonObject
            {
                ["Email"] = new JsonArray("Email must not be empty."),
            },
            ["traceId"] = TraceId,
        };
    }

    private static bool TryGetProblemMedia(OpenApiOperation operation, string status, out OpenApiMediaType media)
    {
        media = null!;

        if (operation.Responses is null
            || !operation.Responses.TryGetValue(status, out var response)
            || response.Content is null
            || !response.Content.TryGetValue("application/problem+json", out var problemMedia)
            || problemMedia is null)
        {
            return false;
        }

        media = problemMedia;
        return true;
    }
}
