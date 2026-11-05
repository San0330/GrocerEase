using System.Reflection;
using System.Runtime.CompilerServices;
using GrocerEase.Application.Authentication;
using GrocerEase.Application.Authentication.Commands;
using GrocerEase.Contracts.Authentication;

namespace GrocerEase.UnitTests.Architecture;

/// <summary>
/// A <c>record</c> writes every property into its <c>ToString()</c>. Anything that logs or interpolates a command or a
/// request would then write the caller's plaintext password, or a bearer token, to the log. These tests keep both out.
/// </summary>
public class PasswordRedactionTests
{
    private const string Secret = "Tr0ub4dor&3-plaintext";

    public static TheoryData<object> RecordsThatCarryAPassword => new()
    {
        new RegisterCommand("Ada", "Lovelace", "ada@example.com", Secret),
        new LoginCommand("ada@example.com", Secret),
        new RegisterRequest("Ada", "Lovelace", "ada@example.com", Secret),
        new LoginRequest("ada@example.com", Secret),
    };

    public static TheoryData<object> RecordsThatCarryAToken => new()
    {
        new AuthenticationResult(Guid.NewGuid(), "Ada", "Lovelace", "ada@example.com", Secret, Secret),
        new AuthenticationResponse(Guid.NewGuid(), "Ada", "Lovelace", "ada@example.com", Secret, Secret),
    };

    public static TheoryData<object> RecordsThatCarryOnlyARefreshToken => new()
    {
        new RefreshTokenCommand(Secret),
        new RefreshRequest(Secret),
    };

    [Theory]
    [MemberData(nameof(RecordsThatCarryAPassword))]
    public void ToString_LeavesThePasswordOut_AndKeepsTheRest(object value)
    {
        var text = value.ToString()!;

        Assert.DoesNotContain(Secret, text);
        Assert.Contains("Password = [redacted]", text);
        // Redacting must not blank the whole line: the other fields are what make the line useful.
        Assert.Contains("ada@example.com", text);
        Assert.StartsWith(value.GetType().Name, text);
    }

    [Theory]
    [MemberData(nameof(RecordsThatCarryAToken))]
    public void ToString_LeavesTheTokenOut_AndKeepsTheRest(object value)
    {
        var text = value.ToString()!;

        Assert.DoesNotContain(Secret, text);
        Assert.Contains("Token = [redacted]", text);
        Assert.Contains("RefreshToken = [redacted]", text);
        Assert.Contains("ada@example.com", text);
        Assert.StartsWith(value.GetType().Name, text);
    }

    [Theory]
    [MemberData(nameof(RecordsThatCarryOnlyARefreshToken))]
    public void ToString_LeavesTheRefreshTokenOut(object value)
    {
        var text = value.ToString()!;

        Assert.DoesNotContain(Secret, text);
        Assert.Contains("RefreshToken = [redacted]", text);
        Assert.StartsWith(value.GetType().Name, text);
    }

    [Fact]
    public void EveryTypeThatCarriesASecret_KeepsItOutOfToString()
    {
        // Finds types by reflection, so the next command (a password change, a reset) is covered without remembering
        // to add it to the list above. Application and Contracts are where outside input arrives as records.
        var assemblies = new[] { Layers.ApplicationAssembly, Layers.ContractsAssembly };

        var carriers = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false, IsPublic: true }
                           && !type.IsDefined(typeof(CompilerGeneratedAttribute)))
            .Select(type => (type, properties: SecretProperties(type)))
            .Where(pair => pair.properties.Length > 0)
            .ToArray();

        // If this finds nothing, the search is broken and the test would pass for the wrong reason.
        Assert.NotEmpty(carriers);

        var leaking = new List<string>();
        foreach (var (type, properties) in carriers)
        {
            // Skips the constructor on purpose: the point is to read the real ToString of any shape of type.
            var instance = RuntimeHelpers.GetUninitializedObject(type);
            foreach (var property in properties)
            {
                property.SetValue(instance, Secret);
            }

            if (instance.ToString()!.Contains(Secret))
            {
                leaking.Add(type.Name);
            }
        }

        Assert.True(leaking.Count == 0, "ToString prints a password or token: " + string.Join(", ", leaking));
    }

    private static PropertyInfo[] SecretProperties(Type type) => type
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property => property.PropertyType == typeof(string)
                           && property.CanWrite
                           && (property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase)
                               || property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase)))
        .ToArray();
}
