using System.Diagnostics;

namespace GrocerEase.Api.MiddleWare;

internal static class RequestTrace
{
    /// <summary>The W3C trace id when the request is being traced, otherwise ASP.NET's own request id.</summary>
    public static string IdFor(HttpContext context) =>
        Activity.Current?.Id ?? context.TraceIdentifier;
}
