using NSchema.Schema.Model.Scripts;

namespace NSchema.Schema.Model;

/// <summary>
/// The desired state read from the project's DDL.
/// </summary>
internal sealed record DesiredProject(DatabaseSchema Schema, IReadOnlyList<Script> Scripts);
