using NSchema.Model;

namespace NSchema.Project.Model.Directives;

/// <summary>
/// A schema drop directive: the schema is explicitly declared dropped.
/// </summary>
/// <param name="Name">The dropped schema's name (its current reality).</param>
public sealed record SchemaDropDirective(SqlIdentifier Name);
