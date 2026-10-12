using System;

namespace Dignite.NotificationCenter;

/// <summary>
/// Produces the unique key of a push device registration. A token is unique per provider, so the key is the provider
/// (case-insensitive, as providers are matched) plus the token (case-sensitive, as issued). Hashing keeps the unique
/// index short whatever the token length — SQL Server caps an index key at 900 bytes.
/// </summary>
public static class PushDeviceIdentity
{
    public static string GetTokenKey(string provider, string token)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentException("The value cannot be null, empty, or whitespace.", nameof(provider));
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("The value cannot be null, empty, or whitespace.", nameof(token));
        }

        return NotificationCenterIdentityKey.Compute("P", provider.ToUpperInvariant(), token);
    }
}
