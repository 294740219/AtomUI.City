using AtomUI.City.Security;

namespace AtomUI.City.Security.Tests;

public sealed class AuthorizationResultTests
{
    [Fact]
    public void ForbiddenAndDeniedUseDistinctFailureKinds()
    {
        var forbidden = AuthorizationResult.Forbidden("settings.write");
        var denied = AuthorizationResult.Denied("custom.requirement");

        Assert.Equal(SecurityFailureKind.Forbidden, forbidden.FailureKind);
        Assert.Equal(AuthorizationResultStatus.Forbidden, forbidden.Status);
        Assert.Equal(SecurityFailureKind.RequirementFailed, denied.FailureKind);
        Assert.Equal(AuthorizationResultStatus.Denied, denied.Status);
    }

    [Fact]
    public void FailedRejectsSuccessFailureKind()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AuthorizationResult.Failed(SecurityFailureKind.None));
    }

    [Theory]
    [InlineData(SecurityFailureKind.AuthenticationRequired)]
    [InlineData(SecurityFailureKind.Forbidden)]
    [InlineData(SecurityFailureKind.RequirementFailed)]
    [InlineData(SecurityFailureKind.Cancelled)]
    public void FailedRejectsKindsOwnedByOtherResultStatuses(SecurityFailureKind failureKind)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AuthorizationResult.Failed(failureKind));
    }

    [Fact]
    public void MessageArgumentsRejectExternalMutation()
    {
        var arguments = new List<object?> { "settings.write" };
        var result = AuthorizationResult.Forbidden("settings.write", messageArguments: arguments);
        var exposedArguments = Assert.IsAssignableFrom<IList<object?>>(result.MessageArguments);

        arguments[0] = "changed";

        Assert.Throws<NotSupportedException>(() => exposedArguments[0] = "changed");
        Assert.Equal("settings.write", result.MessageArguments![0]);
    }
}
