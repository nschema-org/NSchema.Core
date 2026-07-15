namespace NSchema.Project.Nsql.Syntax.Schemas;

/// <summary>
/// <c>PARTIAL SCHEMA name;</c> — the schema's declaration is partial: an object absent from the project is
/// left alone rather than dropped.
/// </summary>
/// <param name="Schema">The declared schema the directive applies to.</param>
public sealed record PartialSchemaStatement(Identifier Schema) : NsqlStatement;
