using NSchema.Resolution;

namespace NSchema.Schema.Serialization;

/// <summary>
/// Resolves <see cref="ISchemaDocumentSerializer"/>s from the set registered in DI, by format.
/// </summary>
internal sealed class DefaultSchemaDocumentSerializerResolver(IEnumerable<ISchemaDocumentSerializer> serializers)
    : KeyedResolver<string, ISchemaDocumentSerializer>(serializers, s => s.Format, "schema serializer"),
      ISchemaDocumentSerializerResolver
{
    public IReadOnlyCollection<string> AvailableFormats => Keys;
    public ISchemaDocumentSerializer ForFormat(string format) => base.Resolve(format);
    public bool TryForFormat(string format, out ISchemaDocumentSerializer? serializer) => TryResolve(format, out serializer);
}
