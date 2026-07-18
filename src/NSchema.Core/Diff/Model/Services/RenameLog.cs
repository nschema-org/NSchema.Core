using NSchema.Model;

namespace NSchema.Diff.Model.Services;

/// <summary>
/// The operations the aligner applied while renaming the old schema objects.
/// </summary>
internal sealed class RenameLog
{
    private readonly IReadOnlyDictionary<SqlIdentifier, SqlIdentifier> _schemas;
    private readonly IReadOnlyDictionary<ObjectIdentity, SqlIdentifier> _objects;
    private readonly IReadOnlyDictionary<MemberAddress, SqlIdentifier> _columns;

    internal RenameLog(
        IReadOnlyDictionary<SqlIdentifier, SqlIdentifier> schemas,
        IReadOnlyDictionary<ObjectIdentity, SqlIdentifier> objects,
        IReadOnlyDictionary<MemberAddress, SqlIdentifier> columns)
    {
        _schemas = schemas;
        _objects = objects;
        _columns = columns;
    }

    /// <summary>
    /// The empty log — nothing was renamed.
    /// </summary>
    public static RenameLog Empty { get; } = new(
        new Dictionary<SqlIdentifier, SqlIdentifier>(),
        new Dictionary<ObjectIdentity, SqlIdentifier>(),
        new Dictionary<MemberAddress, SqlIdentifier>());

    /// <summary>
    /// The previous name of the schema now named <paramref name="declared"/>, or <see langword="null"/> when
    /// it was not renamed.
    /// </summary>
    public SqlIdentifier? RenamedFrom(SqlIdentifier declared) => _schemas.GetValueOrDefault(declared);

    /// <summary>
    /// The previous name of the object now at <paramref name="declared"/>, or <see langword="null"/> when it
    /// was not renamed.
    /// </summary>
    public SqlIdentifier? RenamedFrom(ObjectIdentity declared) => _objects.GetValueOrDefault(declared);

    /// <summary>
    /// The previous name of the column now at <paramref name="declared"/>, or <see langword="null"/> when it
    /// was not renamed.
    /// </summary>
    public SqlIdentifier? RenamedFrom(MemberAddress declared) => _columns.GetValueOrDefault(declared);
}
