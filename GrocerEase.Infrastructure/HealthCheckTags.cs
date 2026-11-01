namespace GrocerEase.Infrastructure;

public static class HealthCheckTags
{
    /// <summary>Checks that must pass before the app can serve traffic, as opposed to merely being alive.</summary>
    public const string Ready = "ready";
}
