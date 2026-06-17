using System.Text;
using NSchema.Schema.Model;

namespace NSchema.Schema.Serialization.Ddl;

/// <summary>
/// Reads and writes a <see cref="DatabaseSchema"/> as canonical NSchema DDL.
/// </summary>
public sealed class DdlSchemaSerializer : ISchemaSerializer
{
    /// <summary>
    /// The format name key for this serializer.
    /// </summary>
    public const string FormatName = "sql";

    /// <summary>
    /// A singleton instance of the <see cref="DdlSchemaSerializer"/> class.
    /// </summary>
    public static readonly DdlSchemaSerializer Instance = new();

    /// <inheritdoc/>
    public async ValueTask<DatabaseSchema> Read(Stream source, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        return new DdlParser(text).Parse();
    }

    /// <inheritdoc/>
    public async ValueTask Write(DatabaseSchema schema, Stream destination, CancellationToken cancellationToken = default)
    {
        var text = DdlSchemaWriter.Write(schema);
        await using var writer = new StreamWriter(destination, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        await writer.WriteAsync(text.AsMemory(), cancellationToken);
    }
}
