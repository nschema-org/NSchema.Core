using NSchema.Configuration;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Scripts;
using NSchema.Schema.Model.Templates;

namespace NSchema.Schema.Ddl.Model;

/// <summary>
/// The full result of parsing a DDL source file.
/// </summary>
public sealed record DdlDocument(DatabaseSchema Schema, IReadOnlyList<ConfigBlock> Config, IReadOnlyList<Script> Scripts)
{
    /// <summary>
    /// The template constructs declared in the document.
    /// </summary>
    public TemplateSet Templates { get; init; } = new();
}
