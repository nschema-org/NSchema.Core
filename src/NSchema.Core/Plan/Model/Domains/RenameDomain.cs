using NSchema.Model;

namespace NSchema.Plan.Model.Domains;

/// <summary>
/// Represents renaming an existing domain.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the domain.</param>
/// <param name="OldName">The current name of the domain.</param>
/// <param name="NewName">The new name of the domain.</param>
public sealed record RenameDomain(SqlIdentifier SchemaName, SqlIdentifier OldName, SqlIdentifier NewName) : MigrationAction;
