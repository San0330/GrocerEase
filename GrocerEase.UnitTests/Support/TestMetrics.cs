using System.Diagnostics.Metrics;
using GrocerEase.Application.Common.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace GrocerEase.UnitTests.Support;

/// <summary>
/// A <see cref="GrocerEaseMetrics"/> on its own meter factory. Each test gets fresh counters, so tests that run in
/// parallel cannot see each other's measurements, which a static meter would allow.
/// </summary>
internal sealed class TestMetrics : IDisposable
{
    private readonly ServiceProvider _provider = new ServiceCollection().AddMetrics().BuildServiceProvider();

    public TestMetrics()
    {
        Factory = _provider.GetRequiredService<IMeterFactory>();
        Metrics = new GrocerEaseMetrics(Factory);
    }

    public IMeterFactory Factory { get; }

    public GrocerEaseMetrics Metrics { get; }

    /// <summary>Starts recording an instrument. Create it before the code under test runs.</summary>
    public MetricCollector<long> Count(string instrument) => new(Factory, GrocerEaseMetrics.MeterName, instrument);

    public MetricCollector<double> Measure(string instrument) => new(Factory, GrocerEaseMetrics.MeterName, instrument);

    /// <summary>The attempts recorded so far as "operation/outcome" strings, in order.</summary>
    public static string[] Attempts(MetricCollector<long> attempts) => attempts.GetMeasurementSnapshot()
        .Select(m => $"{m.Tags["operation"]}/{m.Tags["outcome"]}")
        .ToArray();

    public void Dispose() => _provider.Dispose();
}
