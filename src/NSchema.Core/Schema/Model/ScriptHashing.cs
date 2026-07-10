using System.Security.Cryptography;
using System.Text;

namespace NSchema.Schema.Model;

internal static class ScriptHashing
{
    /// <summary>
    /// Computes the canonical script-body hash (SHA-256 of the UTF-8 text, lowercase hex).
    /// </summary>
    public static string Hash(string sql) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
}
