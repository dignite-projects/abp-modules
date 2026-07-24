using System.Security.Cryptography;
using System.Text;

namespace System.IO;

public static class StreamExtensions
{
    public static string Sha256(this Stream stream)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        var sb = new StringBuilder(64);
        for (var i = 0; i < hash.Length; i++)
        {
            sb.Append(hash[i].ToString("X2"));
        }

        if (stream.Position > 0)
        {
            stream.Position = 0;
        }

        return sb.ToString();
    }

    public static string Md5(this Stream stream)
    {
        var md5 = MD5.Create();
        md5.ComputeHash(stream);
        var hash = md5.Hash!;
        md5.Clear();

        var sb = new StringBuilder(32);
        for (var i = 0; i < hash.Length; i++)
        {
            sb.Append(hash[i].ToString("X2"));
        }

        if (stream.Position > 0)
        {
            stream.Position = 0;
        }

        return sb.ToString();
    }
}
