using NSchema.Project.Nsql.Syntax.Tables;

namespace NSchema.Project.Nsql.Syntax.Constraints;

/// <summary>
/// <c>CONSTRAINT name UNIQUE (columns)</c>.
/// </summary>
/// <param name="Name">The constraint name.</param>
/// <param name="Columns">The unique columns.</param>
public sealed record UniqueDefinition(Identifier Name, IReadOnlyList<Identifier> Columns) : TableMember;