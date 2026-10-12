using System.Collections.Generic;
using Volo.Abp;

namespace Dignite.Abp.Notifications.Push;

/// <summary>One push message for one device.</summary>
public sealed class PushMessage
{
    public string Token { get; }

    /// <summary>Null lets the operating system show the app name instead.</summary>
    public string? Title { get; }

    public string Body { get; }

    /// <summary>The silent payload the app reads when the user taps the notification. See <see cref="PushDataKeys"/>.</summary>
    public IReadOnlyDictionary<string, string> Data { get; }

    public PushMessage(string token, string? title, string body, IReadOnlyDictionary<string, string> data)
    {
        Token = Check.NotNullOrWhiteSpace(token, nameof(token));
        Title = string.IsNullOrWhiteSpace(title) ? null : title;
        Body = Check.NotNull(body, nameof(body));
        Data = Check.NotNull(data, nameof(data));
    }
}
