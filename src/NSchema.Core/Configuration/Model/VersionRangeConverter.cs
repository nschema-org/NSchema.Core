using System.ComponentModel;
using System.Globalization;

namespace NSchema.Configuration.Model;

/// <summary>
/// Converts a version-range string to a <see cref="VersionRange"/>, so the configuration binder can bind one.
/// </summary>
internal sealed class VersionRangeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string text ? VersionRange.Parse(text) : base.ConvertFrom(context, culture, value)!;
}
