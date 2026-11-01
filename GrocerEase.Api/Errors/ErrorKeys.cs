using System.Text.Json;

namespace GrocerEase.Api.Errors;

/// <summary>
/// Names a failing field the way the client wrote it. Requests and responses are camelCase JSON, but validators
/// and model binding report CLR property names (<c>Email</c>, <c>FirstName</c>), so a client would otherwise have to
/// translate them back before it could show an error beside the right input.
/// </summary>
public static class ErrorKeys
{
    /// <summary>
    /// <c>FirstName</c> becomes <c>firstName</c>, and <c>Address.PostCode</c> becomes <c>address.postCode</c>.
    /// A JSON path such as <c>$.email</c> already uses the client's names and is returned unchanged.
    /// </summary>
    public static string ToJsonName(string key)
    {
        if (key.StartsWith('$'))
            return key;

        return string.Join('.', key.Split('.').Select(segment => JsonNamingPolicy.CamelCase.ConvertName(segment)));
    }
}
