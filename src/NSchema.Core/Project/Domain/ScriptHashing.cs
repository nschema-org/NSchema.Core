using System.Security.Cryptography;
using System.Text;
using NSchema.Project.Domain.Models;

namespace NSchema.Project.Domain;

/// <summary>
/// Domain service computing the canonical script-body hash. The hash is opaque outside the schema domain.
/// </summary>
internal static class ScriptHashing
{
    /// <summary>
    /// Computes the canonical script-body hash (SHA-256 of the UTF-8 text, lowercase hex).
    /// </summary>
    public static string Hash(SqlText sql) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sql.Value)));
}
