using System.ComponentModel;
using System.Globalization;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// Converts a label string to a <see cref="PluginLabel"/>, so the configuration binder can bind one.
/// </summary>
internal sealed class PluginLabelConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string text ? new PluginLabel(text) : base.ConvertFrom(context, culture, value)!;
}
