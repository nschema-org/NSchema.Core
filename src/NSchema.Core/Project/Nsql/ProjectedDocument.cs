using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Scripts;

namespace NSchema.Project.Nsql;

/// <summary>
/// One document's projection into the domain.
/// </summary>
internal sealed record ProjectedDocument(DatabaseSchema Schema, IReadOnlyList<Script> Scripts);
