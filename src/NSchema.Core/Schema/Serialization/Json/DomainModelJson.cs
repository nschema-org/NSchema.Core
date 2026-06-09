using System.Text.Json.Serialization.Metadata;

namespace NSchema.Schema.Serialization.Json;

/// <summary>
/// Shared JSON shaping for the domain model that both the user-facing
/// (<see cref="JsonSchemaSerializer"/>) and state (<c>DefaultSchemaStateSerializer</c>) serializers apply.
/// Keeping it here means the model itself carries no serialization attributes.
/// </summary>
internal static class DomainModelJson
{
    /// <summary>
    /// A <see cref="JsonTypeInfo"/> modifier that drops derived, get-only (computed) properties from
    /// serialization. They have no setter so they can never round-trip, and emitting them would bloat
    /// the output. This replaces a per-member <c>[JsonIgnore]</c>, which would bind a serialization
    /// contract onto the shared domain model.
    /// </summary>
    public static void IgnoreComputedProperties(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        for (var i = typeInfo.Properties.Count - 1; i >= 0; i--)
        {
            if (typeInfo.Properties[i].Set is null)
            {
                typeInfo.Properties.RemoveAt(i);
            }
        }
    }
}
