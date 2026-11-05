using GrocerEase.Application.Common.Telemetry;
using GrocerEase.UnitTests.Support;

namespace GrocerEase.UnitTests.Application;

public sealed class GrocerEaseMetricsTests : IDisposable
{
    private readonly TestMetrics _metrics = new();

    public void Dispose() => _metrics.Dispose();

    [Fact]
    public void TheMeterIsNamedSoTheExporterCanSubscribeToIt()
    {
        Assert.Equal("GrocerEase", GrocerEaseMetrics.MeterName);
    }

    [Fact]
    public void AuthAttempt_CountsByOperationAndOutcome()
    {
        var attempts = _metrics.Count("grocerease.auth.attempts");

        _metrics.Metrics.AuthAttempt(GrocerEaseMetrics.Operations.Login, succeeded: true);
        _metrics.Metrics.AuthAttempt(GrocerEaseMetrics.Operations.Login, succeeded: false);
        _metrics.Metrics.AuthAttempt(GrocerEaseMetrics.Operations.Refresh, succeeded: false);

        Assert.Equal(["login/success", "login/failure", "refresh/failure"], TestMetrics.Attempts(attempts));
        Assert.All(attempts.GetMeasurementSnapshot(), m => Assert.Equal(1, m.Value));
    }

    [Fact]
    public void RefreshTokenReused_CountsEachDetection()
    {
        var reuse = _metrics.Count("grocerease.auth.refresh_token_reuse");

        _metrics.Metrics.RefreshTokenReused();
        _metrics.Metrics.RefreshTokenReused();

        Assert.Equal(2, reuse.GetMeasurementSnapshot().Sum(m => m.Value));
    }

    [Fact]
    public void RateLimitRejected_CountsEachRejection()
    {
        var rejected = _metrics.Count("grocerease.auth.ratelimit_rejections");

        _metrics.Metrics.RateLimitRejected();

        Assert.Equal(1, rejected.GetMeasurementSnapshot().Sum(m => m.Value));
    }

    [Fact]
    public void PasswordHashed_RecordsSecondsByOperation()
    {
        var duration = _metrics.Measure("grocerease.password.hash.duration");

        _metrics.Metrics.PasswordHashed(GrocerEaseMetrics.PasswordOperations.Hash, TimeSpan.FromMilliseconds(250));
        _metrics.Metrics.PasswordHashed(GrocerEaseMetrics.PasswordOperations.Verify, TimeSpan.FromMilliseconds(120));

        var measurements = duration.GetMeasurementSnapshot();
        Assert.Equal(0.25, measurements[0].Value, precision: 6);
        Assert.Equal("hash", measurements[0].Tags["operation"]);
        Assert.Equal(0.12, measurements[1].Value, precision: 6);
        Assert.Equal("verify", measurements[1].Tags["operation"]);
    }

    [Fact]
    public void NoInstrumentCarriesAnythingButItsOwnLowCardinalityTags()
    {
        // Counters are summed per tag combination, so an id or an address in a tag would make a series per user.
        var attempts = _metrics.Count("grocerease.auth.attempts");
        _metrics.Metrics.AuthAttempt(GrocerEaseMetrics.Operations.Register, succeeded: true);

        var tags = Assert.Single(attempts.GetMeasurementSnapshot()).Tags;
        Assert.Equal(["operation", "outcome"], tags.Keys.Order());
    }
}
