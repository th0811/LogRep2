using System.Security.Cryptography;
using System.Text;

namespace FfxiTempLogCollector.Core;

public static class HashUtil
{
    public static string ComputeSha1(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        return Convert.ToHexString(SHA1.HashData(bytes)).ToLowerInvariant();
    }

    public static string ComputeSha1(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return ComputeSha1(Encoding.UTF8.GetBytes(value));
    }

    public static string ComputeSha1(
        string prefix,
        byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(bytes);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        hash.AppendData(Encoding.UTF8.GetBytes(prefix));
        hash.AppendData(bytes);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
