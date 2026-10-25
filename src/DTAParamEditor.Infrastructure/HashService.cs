using System.Security.Cryptography;

namespace DTAParamEditor.Infrastructure;

/// <summary>SHA-256 content fingerprints for conflict detection and verification.</summary>
public static class HashService
{
    public static string Sha256Hex(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }

    public static string Sha256HexFile(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream));
    }
}
