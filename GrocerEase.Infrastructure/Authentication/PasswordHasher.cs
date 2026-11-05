using System.Diagnostics;
using GrocerEase.Application.Common.Interfaces.Authentication;
using GrocerEase.Application.Common.Telemetry;
using Microsoft.Extensions.Options;
using BC = BCrypt.Net.BCrypt;

namespace GrocerEase.Infrastructure.Authentication;

public class PasswordHasher : IPasswordHasher
{
    private readonly int _cost;
    private readonly string _standInHash;
    private readonly GrocerEaseMetrics _metrics;

    public PasswordHasher(IOptions<PasswordHashingSettings> options, GrocerEaseMetrics metrics)
    {
        _cost = options.Value.Cost;
        _metrics = metrics;

        // Hashed once, at the current cost, so checking against it costs what checking a real user costs.
        _standInHash = BC.HashPassword(Guid.NewGuid().ToString("N"), _cost, enhancedEntropy: false);
    }

    public string Hash(string password)
    {
        var started = Stopwatch.GetTimestamp();
        var hash = BC.HashPassword(password, _cost, enhancedEntropy: false);
        _metrics.PasswordHashed(GrocerEaseMetrics.PasswordOperations.Hash, Stopwatch.GetElapsedTime(started));
        return hash;
    }

    public bool Verify(string password, string? hash)
    {
        var started = Stopwatch.GetTimestamp();

        // A missing user is checked against a stand-in so it costs the same, and it is timed the same, so the
        // histogram cannot show a gap that the response time hides.
        var matches = hash is not null && BC.Verify(password, hash);
        if (hash is null)
            BC.Verify(password, _standInHash);

        _metrics.PasswordHashed(GrocerEaseMetrics.PasswordOperations.Verify, Stopwatch.GetElapsedTime(started));
        return matches;
    }
}
