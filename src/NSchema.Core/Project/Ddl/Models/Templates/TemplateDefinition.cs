using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Scripts;

namespace NSchema.Project.Ddl.Models.Templates;

/// <summary>
/// A named, reusable group of schema objects or table members.
/// </summary>
/// <param name="Name">The template's name, unique across all DDL sources.</param>
/// <param name="Kind">The granularity the template targets.</param>
/// <param name="Objects">The objects the template declares.</param>
public sealed record TemplateDefinition(SqlIdentifier Name, TemplateKind Kind, SchemaDefinition Objects)
{
    /// <summary>
    /// The schema name that stands in for "the schema this template is applied to" inside a parsed template body.
    /// </summary>
    public static readonly SqlIdentifier TargetSchemaPlaceholder = new("<template>");

    /// <summary>
    /// The <c>INCLUDE</c> members written in this template's table bodies.
    /// </summary>
    public IReadOnlyList<TemplateInclude> Includes { get; init; } = [];

    /// <summary>
    /// The scripts declared in this template's body.
    /// </summary>
    public IReadOnlyList<Script> Scripts { get; init; } = [];
}
