using System.Diagnostics.CodeAnalysis;

namespace NSchema.Schema.Serialization;

/// <summary>
/// Resolves <see cref="ISchemaDocumentSerializer"/>s from the set registered in DI.
/// </summary>
internal sealed class DefaultSchemaDocumentSerializerResolver(IEnumerable<ISchemaDocumentSerializer> serializers) : ISchemaDocumentSerializerResolver
{
    private readonly IReadOnlyList<ISchemaDocumentSerializer> _serializers = [.. serializers];

    /// <inheritdoc/>
    public IReadOnlyCollection<string> AvailableFormats
        => [.. _serializers.Select(s => s.Format).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <inheritdoc/>
    public ISchemaDocumentSerializer ForFormat(string format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        if (TryForFormat(format, out var serializer))
        {
            return serializer;
        }

        var available = AvailableFormats.Count == 0
            ? "none"
            : string.Join(", ", AvailableFormats.Order(StringComparer.OrdinalIgnoreCase));
        throw new InvalidOperationException(
            $"No schema serializer registered for format '{format}'. Available formats: {available}.");
    }

    /// <inheritdoc/>
    public bool TryForFormat(string format, [NotNullWhen(true)] out ISchemaDocumentSerializer? serializer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        // Last-wins: a later registration for the same format shadows earlier ones.
        for (var i = _serializers.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_serializers[i].Format, format, StringComparison.OrdinalIgnoreCase))
            {
                serializer = _serializers[i];
                return true;
            }
        }

        serializer = null;
        return false;
    }
}
