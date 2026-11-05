using System.Diagnostics.Metrics;

namespace GrocerEase.Application.Common.Telemetry;

/// <summary>
/// The application's own measurements, on one meter. Counters answer "how often", the histogram answers "how long";
/// the request rate and latency come from the framework's instrumentation and are not repeated here.
/// </summary>
/// <remarks>
/// Every tag is one of a handful of fixed words. A tag is a series on its own, so an id, an email or an address in one
/// would create a series per user. The outcome is only success or failure: splitting a failed login by reason would let
/// a dashboard tell which emails exist, which the API response deliberately does not.
/// </remarks>
public sealed class GrocerEaseMetrics
{
    /// <summary>What the exporter subscribes to.</summary>
    public const string MeterName = "GrocerEase";

    public static class Operations
    {
        public const string Register = "register";
        public const string Login = "login";
        public const string Refresh = "refresh";
        public const string Logout = "logout";
    }

    public static class PasswordOperations
    {
        public const string Hash = "hash";
        public const string Verify = "verify";
    }

    private readonly Counter<long> _attempts;
    private readonly Counter<long> _refreshTokenReuse;
    private readonly Counter<long> _rateLimitRejections;
    private readonly Histogram<double> _passwordHashDuration;

    // The factory ties the meter to the container, so a test gets its own counters and the host disposes them.
    public GrocerEaseMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _attempts = meter.CreateCounter<long>(
            "grocerease.auth.attempts",
            unit: "{attempt}",
            description: "Register, login, refresh and logout calls, by operation and outcome.");

        _refreshTokenReuse = meter.CreateCounter<long>(
            "grocerease.auth.refresh_token_reuse",
            unit: "{detection}",
            description: "Times a refresh token that had already been replaced was presented again.");

        _rateLimitRejections = meter.CreateCounter<long>(
            "grocerease.auth.ratelimit_rejections",
            unit: "{request}",
            description: "Authentication requests turned away by the rate limiter.");

        // BCrypt at the default cost takes a few hundred milliseconds. The default buckets are meant for milliseconds
        // and would put every measurement in the first one, so these are chosen for seconds.
        _passwordHashDuration = meter.CreateHistogram<double>(
            "grocerease.password.hash.duration",
            unit: "s",
            description: "How long hashing or checking a password took.",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5] });
    }

    public void AuthAttempt(string operation, bool succeeded) =>
        _attempts.Add(1, new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("outcome", succeeded ? "success" : "failure"));

    public void RefreshTokenReused() => _refreshTokenReuse.Add(1);

    public void RateLimitRejected() => _rateLimitRejections.Add(1);

    public void PasswordHashed(string operation, TimeSpan duration) =>
        _passwordHashDuration.Record(duration.TotalSeconds, new KeyValuePair<string, object?>("operation", operation));
}
