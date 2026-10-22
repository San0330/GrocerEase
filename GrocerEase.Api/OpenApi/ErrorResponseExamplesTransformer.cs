using System.Text.Json.Nodes;
using GrocerEase.Domain.Exceptions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace GrocerEase.Api.OpenApi;

public sealed class ErrorResponseExamplesTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var path = context.Description.RelativePath;

        if (path == "auth/register")
        {
            Set(operation, "409", ErrorCodes.DuplicateEmail, 409, "Duplicate Email", "A user with this email already exists.");
            Set(operation, "400", ErrorCodes.DatabaseConstraint, 400, "Database Error", "A database constraint was violated.");
        }

        if (path == "auth/login")
        {
            Set(operation, "401", ErrorCodes.InvalidCredentials, 401, "Unauthorized", "Email or password is invalid.");
        }

        if (path is "auth/register" or "auth/login")
        {
            SetValidation(operation);
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
            ["traceId"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
        };
    }

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
            ["traceId"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
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
