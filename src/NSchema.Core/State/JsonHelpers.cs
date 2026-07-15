using System.Text.Json.Serialization.Metadata;

namespace NSchema.State;

// Each wire format owns its serialization conventions — deliberately not shared across lanes.
internal static class JsonHelpers
{
    /// <summary>
    /// A <see cref="JsonTypeInfo"/> modifier that drops derived, get-only (computed) properties from serialization.
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
