using Volo.Abp;

namespace Dignite.Abp.Notifications.Push;

public enum PushSendStatus
{
    /// <summary>The provider accepted the message. Not a delivery receipt.</summary>
    Succeeded,

    /// <summary>The device can no longer receive pushes; its registration is removed.</summary>
    TokenInvalid,

    /// <summary>The provider rejected the message for another reason. Logged, not retried.</summary>
    Failed
}

/// <summary>What a provider reported for one <see cref="PushMessage"/>.</summary>
public sealed class PushSendResult
{
    public string Token { get; }

    public PushSendStatus Status { get; }

    /// <summary>The provider's error code, when there is one. Never contains the token.</summary>
    public string? Error { get; }

    public PushSendResult(string token, PushSendStatus status, string? error = null)
    {
        Token = Check.NotNullOrWhiteSpace(token, nameof(token));
        Status = status;
        Error = error;
    }

    public static PushSendResult Succeeded(string token) => new(token, PushSendStatus.Succeeded);

    public static PushSendResult TokenInvalid(string token, string? error = null) =>
        new(token, PushSendStatus.TokenInvalid, error);

    public static PushSendResult Failed(string token, string? error) => new(token, PushSendStatus.Failed, error);
}
