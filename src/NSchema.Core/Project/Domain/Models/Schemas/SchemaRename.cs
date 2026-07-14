namespace NSchema.Project.Domain.Models.Schemas;

/// <summary>
/// A schema rename directive.
/// </summary>
/// <param name="From">The schema's current name.</param>
/// <param name="To">The declared name the schema is renamed to.</param>
public sealed record SchemaRename(SqlIdentifier From, SqlIdentifier To);
