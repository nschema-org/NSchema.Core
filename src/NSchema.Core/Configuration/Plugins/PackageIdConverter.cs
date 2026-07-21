using System.ComponentModel;
using System.Globalization;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// Converts a package-id string to a <see cref="PackageId"/>, so the configuration binder can bind one.
/// </summary>
internal sealed class PackageIdConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string text ? new PackageId(text) : base.ConvertFrom(context, culture, value)!;
}
