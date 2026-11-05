namespace GrocerEase.Api.Cors;

public class CorsSettings
{
    public const string SectionName = "Cors";

    /// <summary>
    /// Origins a browser may call the API from, written as scheme, host and optional port with no path or trailing
    /// slash (<c>http://localhost:3000</c>). Empty means no cross-origin access.
    /// </summary>
    public string[] AllowedOrigins { get; init; } = [];
}
