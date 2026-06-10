namespace NSchema.Diff.Model;

/// <summary>
/// A representation of the changes between two database schemas.
/// </summary>
/// <param name="Schemas">The changed schemas.</param>
public sealed record DatabaseDiff(IReadOnlyList<SchemaDiff>? Schemas = null)
{
    /// <summary>
    /// The changed schemas.
    /// </summary>
    public IReadOnlyList<SchemaDiff> Schemas { get; init; } = Schemas ?? [];

    /// <summary>
    /// Gets a value indicating whether the diff contains no changes at all.
    /// </summary>
    public bool IsEmpty => Schemas.Count == 0;

    /// <summary>
    /// Gets the aggregate counts of every changed element, grouped by <see cref="ChangeKind"/>.
    /// </summary>
    public DiffSummary GetSummary()
    {
        var added = 0;
        var modified = 0;
        var removed = 0;

        foreach (var schema in Schemas)
        {
            if (schema.Kind is { } kind)
            {
                Tally(kind);
            }

            foreach (var table in schema.Tables)
            {
                Tally(table.Kind);
                foreach (var column in table.Columns)
                {
                    Tally(column.Kind);
                }

                foreach (var index in table.Indexes)
                {
                    Tally(index.Kind);
                }

                foreach (var pk in table.PrimaryKey)
                {
                    Tally(pk.Kind);
                }

                foreach (var fk in table.ForeignKeys)
                {
                    Tally(fk.Kind);
                }

                foreach (var unique in table.UniqueConstraints)
                {
                    Tally(unique.Kind);
                }

                foreach (var check in table.Checks)
                {
                    Tally(check.Kind);
                }
            }

            foreach (var view in schema.Views)
            {
                Tally(view.Kind);
            }

            foreach (var enumDiff in schema.Enums)
            {
                Tally(enumDiff.Kind);
            }

            foreach (var sequence in schema.Sequences)
            {
                Tally(sequence.Kind);
            }
        }

        return new DiffSummary(added, modified, removed);

        void Tally(ChangeKind kind)
        {
            switch (kind)
            {
                case ChangeKind.Add: added++; break;
                case ChangeKind.Modify: modified++; break;
                case ChangeKind.Remove: removed++; break;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }
    }
}
