using System;
using System.Collections.Generic;
using Volo.Abp;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The checksum a caller may send with an upload to have it verified (<c>upload-checksum-decision.md</c>): SHA-256,
/// as base64 of its 32 bytes - the spelling of RFC 9530's <c>Content-Digest</c>, and the one the module stores and
/// returns, so the string a caller sent is the string it reads back.
/// </summary>
public static class BlobManagementChecksum
{
    public const int Sha256Length = 32;

    /// <summary>The algorithm's name in a <c>Content-Digest</c> header.</summary>
    public const string ContentDigestAlgorithm = "sha-256";

    /// <summary>
    /// The 32 bytes <paramref name="value"/> stands for. Fails with <see cref="BlobManagementErrorCodes.ChecksumInvalid"/>
    /// when it is not base64 of exactly 32 bytes.
    /// </summary>
    public static byte[] ParseSha256(string value)
    {
        Check.NotNull(value, nameof(value));

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(value.Trim());
        }
        catch (FormatException)
        {
            throw new BusinessException(BlobManagementErrorCodes.ChecksumInvalid);
        }

        if (bytes.Length != Sha256Length)
        {
            throw new BusinessException(BlobManagementErrorCodes.ChecksumInvalid);
        }

        return bytes;
    }

    /// <summary>
    /// The SHA-256 a <c>Content-Digest</c> header carries (<c>sha-256=:base64:</c>, RFC 9530), as base64; <c>null</c>
    /// when there is no header. The other members of the dictionary are ignored, as are the parameters of a member. A
    /// header with no <c>sha-256</c> member is refused with <see cref="BlobManagementErrorCodes.ChecksumInvalid"/>: the
    /// caller asked for a check this module cannot make, and it must not pass as if it had.
    /// </summary>
    public static string? ReadContentDigest(IEnumerable<string>? headerValues)
    {
        if (headerValues == null)
        {
            return null;
        }

        var any = false;
        foreach (var headerValue in headerValues)
        {
            // A member's value is a byte sequence between colons, base64, which holds no comma: the dictionary splits there.
            foreach (var member in headerValue.Split(','))
            {
                var text = member.Trim();
                if (text.Length == 0)
                {
                    continue;
                }

                any = true;
                var equals = text.IndexOf('=');
                if (equals <= 0 ||
                    !text.Substring(0, equals).Trim().Equals(ContentDigestAlgorithm, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = text.Substring(equals + 1);
                var parameters = value.IndexOf(';');
                if (parameters >= 0)
                {
                    value = value.Substring(0, parameters);
                }

                value = value.Trim();
                if (value.Length < 2 || value[0] != ':' || value[value.Length - 1] != ':')
                {
                    throw new BusinessException(BlobManagementErrorCodes.ChecksumInvalid);
                }

                var base64 = value.Substring(1, value.Length - 2);
                ParseSha256(base64);
                return base64;
            }
        }

        return any ? throw new BusinessException(BlobManagementErrorCodes.ChecksumInvalid) : null;
    }
}
