using System.Net;
using AtomUI.City.Data;

namespace AtomUI.City.Data.Tests;

public sealed class DataResultTests
{
    [Fact]
    public void SuccessResultContainsValue()
    {
        var result = DataResult<string>.Success("loaded");

        Assert.True(result.Succeeded);
        Assert.Equal(DataResultStatus.Success, result.Status);
        Assert.Equal("loaded", result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void FailedResultContainsStandardError()
    {
        var error = new DataError(DataErrorKind.NotFound, "The item was not found.");

        var result = DataResult<string>.Failed(error);

        Assert.False(result.Succeeded);
        Assert.Equal(DataResultStatus.Failed, result.Status);
        Assert.Null(result.Value);
        Assert.Equal(DataErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public void DataErrorCarriesLocalizableMessageMetadata()
    {
        var error = new DataError(
            DataErrorKind.AuthorizationForbidden,
            "Permission 'settings.write' is required.",
            MessageKey: "Errors.AuthorizationForbidden",
            MessageArguments: ["settings.write"]);

        var result = DataResult<string>.Failed(error);

        Assert.Equal("Errors.AuthorizationForbidden", result.Error?.MessageKey);
        Assert.Equal(["settings.write"], result.Error?.MessageArguments);
    }

    [Fact]
    public void DataErrorMessageArgumentsRejectExternalMutation()
    {
        var arguments = new List<object?> { "settings.write" };
        var error = new DataError(
            DataErrorKind.AuthorizationForbidden,
            "Permission 'settings.write' is required.",
            MessageKey: "Errors.AuthorizationForbidden",
            MessageArguments: arguments);
        var exposedArguments = Assert.IsAssignableFrom<IList<object?>>(error.MessageArguments);

        arguments[0] = "changed";

        Assert.Throws<NotSupportedException>(() => exposedArguments[0] = "changed");
        Assert.Equal("settings.write", error.MessageArguments![0]);
    }

    [Fact]
    public void DataErrorInitMessageArgumentsRejectExternalMutation()
    {
        var arguments = new List<object?> { "settings.write" };
        var error = new DataError(
            DataErrorKind.AuthorizationForbidden,
            "Permission 'settings.write' is required.",
            MessageKey: "Errors.AuthorizationForbidden")
        {
            MessageArguments = arguments,
        };
        var exposedArguments = Assert.IsAssignableFrom<IList<object?>>(error.MessageArguments);

        arguments[0] = "changed";

        Assert.Throws<NotSupportedException>(() => exposedArguments[0] = "changed");
        Assert.Equal("settings.write", error.MessageArguments![0]);
    }

    [Fact]
    public void DataErrorRejectsBlankMessage()
    {
        Assert.Throws<ArgumentException>(() => new DataError(DataErrorKind.Unknown, " "));
        Assert.Throws<ArgumentException>(() =>
            new DataError(DataErrorKind.Unknown, "Unknown error.", TransportStatus: " "));
        Assert.Throws<ArgumentException>(() =>
            new DataError(DataErrorKind.Unknown, "Unknown error.", MessageKey: " "));
    }

    [Fact]
    public void DataErrorRejectsUnknownKind()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DataError((DataErrorKind)999, "Unknown error."));
    }

    [Fact]
    public void DataErrorWithExpressionRejectsInvalidState()
    {
        var error = new DataError(DataErrorKind.Unknown, "Unknown error.");

        Assert.Throws<ArgumentException>(() => error with { Message = "" });
        Assert.Throws<ArgumentOutOfRangeException>(() => error with { Kind = (DataErrorKind)999 });
    }

    [Fact]
    public void HttpStatusMappingIncludesLocalizableMessageKey()
    {
        var error = DataErrorMapper.FromHttpStatusCode(HttpStatusCode.Forbidden);

        Assert.Equal(DataErrorKind.AuthorizationForbidden, error.Kind);
        Assert.Equal("Errors.AuthorizationForbidden", error.MessageKey);
    }

    [Fact]
    public void HttpStatusMappingPreservesServiceUnavailable()
    {
        var error = DataErrorMapper.FromHttpStatusCode(HttpStatusCode.ServiceUnavailable);

        Assert.Equal(DataErrorKind.ServiceUnavailable, error.Kind);
        Assert.Equal("Errors.ServiceUnavailable", error.MessageKey);
    }

    [Fact]
    public void CancelledResultIsNotFailure()
    {
        var result = DataResult<string>.Cancelled();

        Assert.False(result.Succeeded);
        Assert.Equal(DataResultStatus.Cancelled, result.Status);
        Assert.Null(result.Value);
        Assert.Equal(DataErrorKind.Cancelled, result.Error?.Kind);
    }

    [Fact]
    public void StaleSuppressedResultUsesDedicatedStatus()
    {
        var result = DataResult<string>.StaleSuppressed();

        Assert.False(result.Succeeded);
        Assert.Equal(DataResultStatus.StaleSuppressed, result.Status);
        Assert.Null(result.Value);
        Assert.Equal(DataErrorKind.Cancelled, result.Error?.Kind);
    }

    [Fact]
    public void PartialResultPreservesValueAndErrorAcrossCompatibleCast()
    {
        var error = new DataError(DataErrorKind.StreamProtocolError, "Some records were unavailable.");
        var result = DataResult<string>.Partial("available", error);

        var cast = result.Cast<object>();

        Assert.Equal(DataResultStatus.Partial, cast.Status);
        Assert.False(cast.Succeeded);
        Assert.Equal("available", cast.Value);
        Assert.Same(error, cast.Error);
    }

    [Fact]
    public void SuccessCastPreservesNullableNullValue()
    {
        var result = DataResult<string?>.Success(null);

        var cast = result.Cast<object?>();

        Assert.Equal(DataResultStatus.Success, cast.Status);
        Assert.True(cast.Succeeded);
        Assert.Null(cast.Value);
        Assert.Null(cast.Error);
    }

    [Fact]
    public void PartialCastPreservesNullableNullValueAndError()
    {
        var error = new DataError(DataErrorKind.StreamProtocolError, "Partial response.");
        var result = DataResult<string?>.Partial(null, error);

        var cast = result.Cast<object?>();

        Assert.Equal(DataResultStatus.Partial, cast.Status);
        Assert.Null(cast.Value);
        Assert.Same(error, cast.Error);
    }

    [Fact]
    public void ErrorMappersRejectSuccessfulAndUnknownStatuses()
    {
        Assert.Throws<ArgumentException>(() => DataErrorMapper.FromHttpStatusCode(HttpStatusCode.OK));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DataErrorMapper.FromHttpStatusCode((HttpStatusCode)0));
        Assert.Throws<ArgumentException>(() => DataErrorMapper.FromGrpcStatus(GrpcStatusCode.OK));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DataErrorMapper.FromGrpcStatus((GrpcStatusCode)999));
    }
}
