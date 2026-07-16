namespace NSchema.Model.Scripts;

/// <summary>
/// The address of a script: the schema its run is scoped to, and its name.
/// </summary>
/// <remarks>
/// Unlike an <see cref="ObjectReference"/>, the container is genuinely optional: a script with no scope schema
/// is global — it lives at the project root, not in any schema. Global scripts are unique among global scripts;
/// scoped scripts are unique within their schema.
/// </remarks>
/// <param name="Schema">The schema the script's run is scoped to, or <see langword="null"/> when the script is global.</param>
/// <param name="Name">The script's declared name.</param>
public sealed record ScriptReference(SqlIdentifier? Schema, SqlIdentifier Name) : Address
{
    /// <inheritdoc />
    public override string Value => Schema == null ? Name.Value : $"{Schema}.{Name}";
}
