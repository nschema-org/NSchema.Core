using NSchema.Project.Domain.Models;
using NSchema.Project.Nsql.Syntax.Tables;

namespace NSchema.Project.Nsql.Syntax.Constraints;

/// <summary>
/// <c>CONSTRAINT name CHECK (expression)</c> — in a table body or a domain declaration.
/// </summary>
/// <param name="Name">The constraint name.</param>
/// <param name="Expression">The boolean expression the constraint enforces.</param>
public sealed record CheckDefinition(Identifier Name, SqlText Expression) : TableMember;