using System.Text;
using NSchema.Configuration;

namespace NSchema.Schema.Serialization.Ddl;

/// <summary>
/// Reads the top-level configuration blocks from NSchema DSL source, ignoring the schema statements.
/// </summary>
public static class DslConfigReader
{
    /// <summary>
    /// Parses <paramref name="source"/> and returns its configuration blocks in declaration order.
    /// </summary>
    public static IReadOnlyList<ConfigBlock> Read(string source) =>
        new DslParser(source).ParseDocument().Config;

    /// <summary>
    /// Reads <paramref name="source"/> to the end and parses its configuration blocks in declaration order.
    /// </summary>
    public static async ValueTask<IReadOnlyList<ConfigBlock>> Read(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        return Read(text);
    }
}
