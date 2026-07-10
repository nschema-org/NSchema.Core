using System.Security.Cryptography;
using System.Text;

namespace NSchema.Schema;

/// <summary>
/// Domain service computing the canonical script-body hash. The hash is opaque outside the schema domain.
/// </summary>
internal static class ScriptHashing
{
    /// <summary>
    /// Computes the canonical script-body hash (SHA-256 of the UTF-8 text, lowercase hex).
    /// </summary>
    public static string Hash(string sql) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
}
