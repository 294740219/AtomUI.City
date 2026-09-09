using System.Reflection;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Security;

namespace AtomUI.City.Security.Tests;

public sealed class SecurityDiagnosticTests
{
    [Fact]
    public void DiagnosticIdsAreUniqueAndUseStablePrefix()
    {
        var ids = typeof(SecurityDiagnosticIds)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(static field => Assert.IsType<string>(field.GetRawConstantValue()))
            .ToArray();

        Assert.NotEmpty(ids);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, static id => Assert.Matches("^AUCSEC[0-9]{3}$", id));
    }

    [Fact]
    public void DiagnosticSinkFailureDoesNotRollBackAuthenticationState()
    {
        var store = new AuthenticationStateStore(new ThrowingHostDiagnostics());

        var snapshot = store.SetAnonymous();

        Assert.Equal(AuthenticationState.Anonymous, snapshot.State);
        Assert.Equal(1, snapshot.Revision);
    }

    [Fact]
    public void PermissionClaimTypeIsAStablePublicContract()
    {
        Assert.Equal("permission", SecurityClaimTypes.Permission);
    }

    private sealed class ThrowingHostDiagnostics : IHostDiagnostics
    {
        public IReadOnlyList<HostDiagnosticRecord> Records => [];

        public void Write(HostDiagnosticRecord record)
        {
            throw new InvalidOperationException("diagnostics unavailable");
        }

        public void Complete()
        {
        }
    }
}
