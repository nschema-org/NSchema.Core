using NSchema.Schema.Model.Migrations;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Scripts;

namespace NSchema.Schema.Model.Templates;

/// <summary>
/// A named, reusable group of schema objects or table members.
/// </summary>
/// <param name="Name">The template's name, unique across all DDL sources.</param>
/// <param name="Kind">The granularity the template targets.</param>
/// <param name="Objects">The objects the template declares.</param>
public sealed record TemplateDefinition(string Name, TemplateKind Kind, SchemaDefinition Objects)
{
    /// <summary>
    /// The schema name that stands in for "the schema this template is applied to" inside a parsed template body.
    /// </summary>
    public const string TargetSchemaPlaceholder = "<template>";

    /// <summary>
    /// The <c>INCLUDE</c> members written in this template's table bodies.
    /// </summary>
    public IReadOnlyList<TemplateInclude> Includes { get; init; } = [];

    /// <summary>
    /// The data migrations declared in this template's body.
    /// </summary>
    public IReadOnlyList<DataMigration> Migrations { get; init; } = [];

    /// <summary>
    /// The deployment scripts declared in this template's body.
    /// </summary>
    public IReadOnlyList<Script> Scripts { get; init; } = [];
}
