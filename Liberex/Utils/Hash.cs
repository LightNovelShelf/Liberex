using System.Security.Cryptography;

namespace Liberex.Utils;

public class Hash
{
    public static string ComputeMD5(byte[] messageBytes)
    {
        using var hash = MD5.Create();
        byte[] hashMessage = hash.ComputeHash(messageBytes);
        return BitConverter.ToString(hashMessage).Replace("-", "").ToLower();
    }

    public static async ValueTask<string> ComputeMD5Async(Stream stream, CancellationToken cancellationToken = default)
    {
        using var hash = MD5.Create();
        byte[] hashMessage = await hash.ComputeHashAsync(stream, cancellationToken);
        return BitConverter.ToString(hashMessage).Replace("-", "").ToLower();
    }

    public static string ComputeSha1(byte[] messageBytes)
    {
        using var hash = SHA1.Create();
        byte[] hashMessage = hash.ComputeHash(messageBytes);
        return BitConverter.ToString(hashMessage).Replace("-", "").ToLower();
    }
}