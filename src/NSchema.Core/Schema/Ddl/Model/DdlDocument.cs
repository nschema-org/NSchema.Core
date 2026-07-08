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
    /// The template definitions declared in the document.
    /// Definitions are inert until an application names them.
    /// </summary>
    public IReadOnlyList<TemplateDefinition> Templates { get; init; } = [];

    /// <summary>
    /// The template applications declared in the document.
    /// </summary>
    public IReadOnlyList<TemplateApplication> Applications { get; init; } = [];
}
