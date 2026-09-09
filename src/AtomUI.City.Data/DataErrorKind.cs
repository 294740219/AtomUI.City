namespace AtomUI.City.Data;

public enum DataErrorKind
{
    Cancelled,
    Timeout,
    NetworkUnavailable,
    CredentialUnavailable,
    AuthenticationRequired,
    AuthenticationExpired,
    AuthorizationForbidden,
    BadRequest,
    NotFound,
    Conflict,
    ValidationFailed,
    ServerError,
    ServiceUnavailable,
    TransportError,
    SerializationError,
    PolicyRejected,
    ConnectionFailed,
    ConnectionClosed,
    ReconnectFailed,
    StreamCancelled,
    [Obsolete("Normal stream completion is not a data error and must not produce a failed DataResult.")]
    StreamCompleted,
    StreamProtocolError,
    DeadlineExceeded,
    Unavailable,
    PluginUnavailable,
    LocalStorageError,
    Unknown,
}
