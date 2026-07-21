using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using NSchema.Plugins.Model.Config;

namespace NSchema.Plugins;

/// <summary>
/// Binds a <see cref="PluginConfig"/>'s attributes onto an options object.
/// </summary>
/// <remarks>
/// Snake_case attribute names match property names (underscores and case ignored).
/// Dotted keys walk into nested objects, <c>required</c> members must be set.
/// </remarks>
internal static class PluginConfigBinder
{
    public static Result<T> Bind<T>(PluginConfig config)
    {
        var instance = Activator.CreateInstance<T>()!;
        var diagnostics = new List<Diagnostic>();
        var bound = new HashSet<string>();

        foreach (var (key, value) in config.Attributes)
        {
            BindAttribute(instance, key, value, diagnostics, bound);
        }

        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<RequiredMemberAttribute>() is not null && !bound.Contains(property.Name))
            {
                diagnostics.Add(PluginDiagnostics.MissingRequiredOption(AttributeKey.ForProperty(property.Name), typeof(T)));
            }
        }

        return Result.From(instance, diagnostics);
    }

    private static void BindAttribute(object root, AttributeKey key, ConfigValue value, List<Diagnostic> diagnostics, HashSet<string> bound)
    {
        var segments = key.Segments;
        var target = root;
        for (var i = 0; i < segments.Count; i++)
        {
            var property = FindProperty(target.GetType(), segments[i]);
            if (property is null)
            {
                diagnostics.Add(PluginDiagnostics.UnknownOption(key, root.GetType()));
                return;
            }
            if (target == root)
            {
                bound.Add(property.Name);
            }

            if (i == segments.Count - 1)
            {
                if (TryConvert(value, property.PropertyType, out var converted))
                {
                    property.SetValue(target, converted);
                }
                else
                {
                    diagnostics.Add(PluginDiagnostics.UnbindableOption(key, value.Kind, property.PropertyType));
                }
                return;
            }

            // An intermediate segment: get or create the nested options object and descend into it.
            if (property.GetValue(target) is not { } nested)
            {
                if (property.PropertyType.GetConstructor(Type.EmptyTypes) is null)
                {
                    diagnostics.Add(PluginDiagnostics.UnknownOption(key, root.GetType()));
                    return;
                }
                nested = Activator.CreateInstance(property.PropertyType)!;
                property.SetValue(target, nested);
            }
            target = nested;
        }
    }

    private static PropertyInfo? FindProperty(Type type, AttributeKey segment) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.CanWrite && segment.Matches(p.Name));

    private static bool TryConvert(ConfigValue value, Type targetType, out object? converted)
    {
        converted = null;
        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        try
        {
            switch (value.Kind)
            {
                case ConfigValueKind.Boolean:
                    if (type != typeof(bool))
                    {
                        return false;
                    }
                    converted = value.AsBoolean();
                    return true;
                case ConfigValueKind.Integer:
                    if (type == typeof(string) || type == typeof(bool) || type.IsEnum || !typeof(IConvertible).IsAssignableFrom(type))
                    {
                        return false;
                    }
                    converted = Convert.ChangeType(value.AsInteger(), type, CultureInfo.InvariantCulture);
                    return true;
                default: // String or Identifier
                    var text = value.AsString();
                    if (type == typeof(string))
                    {
                        converted = text;
                        return true;
                    }
                    if (type.IsEnum)
                    {
                        // read_committed names ReadCommitted: the attribute-key convention binds enum members too.
                        AttributeKey written = text;
                        if (Enum.GetNames(type).FirstOrDefault(written.Matches) is not { } name)
                        {
                            return false;
                        }
                        converted = Enum.Parse(type, name);
                        return true;
                    }
                    var converter = TypeDescriptor.GetConverter(type);
                    if (!converter.CanConvertFrom(typeof(string)))
                    {
                        return false;
                    }
                    converted = converter.ConvertFromInvariantString(text);
                    return true;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Overflow, unparseable text, converter failure — all the same finding: the value doesn't fit.
            return false;
        }
    }
}
