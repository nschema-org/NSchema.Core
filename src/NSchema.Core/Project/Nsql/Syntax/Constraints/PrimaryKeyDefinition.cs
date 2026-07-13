using NSchema.Project.Nsql.Syntax.Tables;

namespace NSchema.Project.Nsql.Syntax.Constraints;

/// <summary>
/// <c>CONSTRAINT name PRIMARY KEY (columns)</c>.
/// </summary>
/// <param name="Name">The constraint name.</param>
/// <param name="Columns">The key columns.</param>
public sealed record PrimaryKeyDefinition(Identifier Name, IReadOnlyList<Identifier> Columns) : TableMember;
