namespace NSchema.Project.Nsql.Syntax.Views;

/// <summary>
/// <c>DROP [MATERIALIZED] VIEW schema.name;</c> — both spellings record a dropped view (the kind is
/// resolved from the current state when the drop is planned).
/// </summary>
/// <param name="Name">The dropped view.</param>
public sealed record DropViewStatement(QualifiedName Name) : NsqlStatement;
