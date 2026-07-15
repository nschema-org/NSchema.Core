namespace NSchema.Model.Views;

/// <summary>
/// A reference from a view to another schema object (a table or another view) that it reads.
/// </summary>
/// <param name="Schema">The schema the referenced object belongs to.</param>
/// <param name="Name">The name of the referenced object.</param>
public sealed record ViewDependency(SqlIdentifier Schema, SqlIdentifier Name);
