using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

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
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(Attributes.Select(a => new KeyValuePair<string, string?>(BinderKey(a.Key), a.Value)))
            .Build();

        T instance;
        try
        {
            // An empty block binds to null; fall back to a blank instance so [Required] still reports the gaps.
            instance = configuration.Get<T>(options => options.ErrorOnUnknownConfiguration = !ignoreUnknown) ?? Activator.CreateInstance<T>();
        }
        catch (InvalidOperationException exception)
        {
            // The binder rejects an unknown key or a value that will not convert; surface it, not a crash.
            return Diagnostic.Error(Source, exception.Message);
        }

        var context = new ValidationContext(instance);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, context, results, validateAllProperties: true);

        return Result.From(instance, results.Select(result => Diagnostic.Error(Source, result.ErrorMessage ?? "Invalid configuration.")));
    }

    // The configuration binder matches keys to property names but does not fold snake_case; convert each dotted
    // segment to PascalCase (connection_string → ConnectionString) and map the dots onto its ':' hierarchy separator.
    private static string BinderKey(string key) =>
        string.Join(':', key.Split('.').Select(PascalCase));

    private static string PascalCase(string segment) =>
        string.Concat(segment.Split('_').Select(part => part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..]));
}
