using System.Security.Cryptography;
using System.Text;

namespace NSchema.Sql.Model;

/// <summary>
/// Identifies a run-once script pending in a plan.
/// </summary>
/// <param name="Name">The script's declared name.</param>
/// <param name="Hash">The hash of the script's SQL body, as produced by <see cref="HashSql"/>.</param>
public sealed record ScriptHash(string Name, string Hash)
{
    /// <summary>
    /// Computes the canonical hash of a script body (SHA-256 of the UTF-8 text, lowercase hex).
    /// </summary>
    public static string HashSql(string sql) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
}
