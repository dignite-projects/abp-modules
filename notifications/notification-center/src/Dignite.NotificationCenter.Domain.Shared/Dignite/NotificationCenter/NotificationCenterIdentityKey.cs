using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Dignite.NotificationCenter;

/// <summary>
/// The one hashing scheme behind this module's non-null, fixed-length identity keys (subscription scopes, push device
/// tokens). Each part is length-prefixed before hashing, so no two different part lists produce the same input.
/// </summary>
internal static class NotificationCenterIdentityKey
{
    public static string Compute(params string[] parts)
    {
        var canonical = new StringBuilder();
        foreach (var part in parts)
        {
            canonical.Append(part.Length.ToString(CultureInfo.InvariantCulture));
            canonical.Append(':');
            canonical.Append(part);
        }

        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
        var result = new StringBuilder(NotificationCenterConsts.SubscriptionIdentityKeyLength);
        foreach (var value in bytes)
        {
            result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        }

        return result.ToString();
    }
}
