using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GrocerEase.Api.Errors;

/// <summary>
/// Answers requests that MVC model binding rejects (broken JSON, a wrong type, a missing required property,
/// an empty body) with the same <see cref="ValidationErrorResponse"/> shape the rest of the API uses.
/// Without this, <c>[ApiController]</c> short-circuits with its own <c>ValidationProblemDetails</c> and the
/// request never reaches <c>ErrorHandlingMiddleware</c> or the FluentValidation validators.
/// </summary>
public static class MalformedRequestResponse
{
    private const string DefaultMessage = "The input was not valid.";

    // Errors keyed by a JSON path ("$", "$.email") come straight from System.Text.Json and carry parser
    // internals: byte offsets and the CLR type name of the request record. The path stays as the key;
    // the text is replaced.
    private const string JsonMessage = "The value is not valid JSON or has the wrong type.";

    private const string RootKey = "$";

    public static IActionResult Create(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToDictionary(
                entry => entry.Key.Length == 0 ? RootKey : ErrorKeys.ToJsonName(entry.Key),
                entry => entry.Value!.Errors
                    .Select(error => MessageFor(entry.Key, error))
                    .Distinct()
                    .ToArray());

        // When the body itself cannot be read MVC also reports the bound parameter ("request") as missing.
        // That repeats the real error under a name the client never sent, so drop it.
        if (errors.Count > 1)
        {
            foreach (var parameter in context.ActionDescriptor.Parameters)
                errors.Remove(parameter.Name);
        }

        var body = ApiProblem.Validation(
            context.HttpContext,
            ApiErrorCodes.MalformedRequest,
            StatusCodes.Status400BadRequest,
            "Malformed Request",
            errors);

        // JsonResult, not ObjectResult: the controllers' [Produces("application/json")] rewrites an
        // ObjectResult's content type, which would turn this into plain application/json.
        return new JsonResult(body)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentType = ApiProblem.ContentType,
        };
    }

    private static string MessageFor(string key, ModelError error)
    {
        if (key.StartsWith('$'))
            return JsonMessage;

        return string.IsNullOrEmpty(error.ErrorMessage) ? DefaultMessage : error.ErrorMessage;
    }
}
