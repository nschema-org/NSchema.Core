using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// The settings a plugin is handed: a block's attributes as a flat key/value map (dotted keys nest), with its label.
/// </summary>
/// <param name="Label">The optional bare-identifier label following the keyword (e.g. <c>"postgres"</c> in <c>DATABASE postgres</c>).</param>
/// <param name="Attributes">The block's attributes, keyed as written (case-insensitive).</param>
public sealed record PluginSettings(PluginLabel? Label, IReadOnlyDictionary<string, string?> Attributes)
{
    private const string Source = "settings";

    /// <summary>
    /// The value of the named attribute as written, or <see langword="null"/> if the settings do not declare it.
    /// </summary>
    public string? Attribute(string name) => Attributes.GetValueOrDefault(name);

    /// <summary>
    /// Gets the attributes in the form of a new <typeparamref name="T"/>, validating its data annotations. snake_case keys
    /// match properties (<c>connection_string</c> → <c>ConnectionString</c>), dotted keys nest (<c>pool.max</c> →
    /// <c>Pool.Max</c>), and identifiers map to enum members. A value that does not fit and a <c>[Required]</c>/<c>[Range]</c>
    /// failure are error diagnostics; an attribute that matches no property is too unless <paramref name="ignoreUnknown"/>.
    /// The result still carries the best-effort instance.
    /// </summary>
    /// <param name="ignoreUnknown">Whether to accept attributes that match no property (for forward-compatible formats), rather than reject them.</param>
    public Result<T> Get<T>(bool ignoreUnknown = false) where T : notnull
    {
        // A blank instance the attributes mutate in place, so a partial bind still reports [Required] gaps and rides the result.
        var instance = Activator.CreateInstance<T>();
        var diagnostics = new List<Diagnostic>();

        foreach (var (key, value) in Attributes)
        {
            Assign(instance, key.Split('.'), depth: 0, key, value, ignoreUnknown, diagnostics);
        }

        var context = new ValidationContext(instance);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, context, results, validateAllProperties: true);

        return Result.From(instance, diagnostics.Concat(results.Select(result => Diagnostic.Error(Source, result.ErrorMessage ?? "Invalid configuration."))));
    }

    // Walks the dotted key onto its property path, creating intermediate objects, then converts and sets the leaf.
    private static void Assign(object target, string[] path, int depth, string key, string? value, bool ignoreUnknown, List<Diagnostic> diagnostics)
    {
        var property = target.GetType().GetProperty(PascalCase(path[depth]), BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property is null)
        {
            if (!ignoreUnknown)
            {
                diagnostics.Add(Diagnostic.Error(Source, $"Unknown attribute '{key}'."));
            }

            return;
        }

        if (depth == path.Length - 1)
        {
            if (TryConvert(property.PropertyType, value, out var converted))
            {
                property.SetValue(target, converted);
            }
            else
            {
                diagnostics.Add(Diagnostic.Error(Source, $"Value '{value}' cannot be assigned to '{key}'."));
            }

            return;
        }

        var child = property.GetValue(target) ?? Activator.CreateInstance(property.PropertyType);
        property.SetValue(target, child);
        Assign(child!, path, depth + 1, key, value, ignoreUnknown, diagnostics);
    }

    // Converts a written value to the property's type: null onto anything nullable, enums by member name (case-insensitive),
    // everything else through its type converter (which resolves any [TypeConverter], e.g. the value objects').
    private static bool TryConvert(Type type, string? value, out object? result)
    {
        result = null;
        var target = Nullable.GetUnderlyingType(type) ?? type;

        if (value is null)
        {
            return !type.IsValueType || target != type;
        }

        if (target.IsEnum)
        {
            return Enum.TryParse(target, value, ignoreCase: true, out result);
        }

        try
        {
            result = TypeDescriptor.GetConverter(target).ConvertFromInvariantString(value);
            return true;
        }
        catch (Exception)
        {
            // Any converter failure (a value that does not fit) is a diagnostic, not a crash.
            return false;
        }
    }

    private static string PascalCase(string segment) =>
        string.Concat(segment.Split('_').Select(part => part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..]));
}
