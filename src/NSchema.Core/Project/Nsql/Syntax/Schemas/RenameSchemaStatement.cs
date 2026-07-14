namespace NSchema.Project.Nsql.Syntax.Schemas;

/// <summary>
/// <c>RENAME SCHEMA name TO name;</c>
/// </summary>
/// <param name="From">The schema's current name.</param>
/// <param name="To">The name the schema is renamed to.</param>
public sealed record RenameSchemaStatement(Identifier From, Identifier To) : NsqlStatement;
