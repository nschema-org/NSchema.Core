using NSchema.Schema.Model.Schemas;

namespace NSchema.Schema.Model.Templates;

/// <summary>
/// A named, reusable group of schema objects.
/// </summary>
/// <param name="Name">The template's name, unique across all DDL sources.</param>
/// <param name="Objects">The objects the template declares.</param>
public sealed record TemplateDefinition(string Name, SchemaDefinition Objects)
{
    /// <summary>
    /// The schema name that stands in for "the schema this template is applied to" inside a parsed template body.
    /// </summary>
    public const string TargetSchemaPlaceholder = "<template>";
}
