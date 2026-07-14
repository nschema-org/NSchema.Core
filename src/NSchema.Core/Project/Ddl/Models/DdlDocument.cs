using NSchema.Project.Ddl.Models.Templates;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Scripts;

namespace NSchema.Project.Ddl.Models;

/// <summary>
/// The full result of parsing a DDL source file.
/// </summary>
public sealed record DdlDocument(DatabaseSchema Schema, IReadOnlyList<Script> Scripts)
{
    /// <summary>
    /// The template constructs declared in the document.
    /// </summary>
    public TemplateSet Templates { get; init; } = new();
}
