using System.Collections;
using AtomUI.City.Security;

namespace AtomUI.City.Security.Tests;

public sealed class AuthorizationPolicyTests
{
    [Fact]
    public void ConstructorRejectsEmptyRequirementsToPreventFailOpenPolicy()
    {
        Assert.Throws<ArgumentException>(() => new AuthorizationPolicy("Empty", []));
    }

    [Fact]
    public void ConstructorRejectsNullRequirement()
    {
        Assert.Throws<ArgumentException>(() => new AuthorizationPolicy(
            "Invalid",
            new AuthorizationRequirement[] { null! }));
    }

    [Fact]
    public void RequirementsRejectExternalListMutation()
    {
        var policy = new AuthorizationPolicy(
            "CanManageSettings",
            [
                AuthorizationRequirement.RequireAuthenticated(),
                AuthorizationRequirement.RequirePermission("settings.manage"),
            ]);
        var requirements = Assert.IsAssignableFrom<IList<AuthorizationRequirement>>(policy.Requirements);

        Assert.Throws<NotSupportedException>(() => requirements[0] = AuthorizationRequirement.RequireRole("admin"));
        Assert.Equal(AuthorizationRequirementKind.Authenticated, policy.Requirements[0].Kind);
        Assert.Equal("settings.manage", policy.Requirements[1].Name);
    }

    [Fact]
    public void ConstructorValidatesTheCapturedRequirementSnapshot()
    {
        Assert.Throws<ArgumentException>(() => new AuthorizationPolicy(
            "Inconsistent",
            new CountOnlyRequirements()));
    }

    private sealed class CountOnlyRequirements : IReadOnlyCollection<AuthorizationRequirement>
    {
        public int Count => 1;

        public IEnumerator<AuthorizationRequirement> GetEnumerator()
        {
            return Enumerable.Empty<AuthorizationRequirement>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
