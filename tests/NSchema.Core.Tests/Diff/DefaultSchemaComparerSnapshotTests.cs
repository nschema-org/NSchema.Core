using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Diff;
using NSchema.Schema.Model;

namespace NSchema.Tests.Diff;

/// <summary>
/// Snapshot coverage for <see cref="DefaultSchemaComparer"/>. Demonstrates Verify diffing a complex
/// object graph: the comparer's whole <c>MigrationDiff</c> tree is serialized and pinned, so a change
/// to the projection (a new field, a reordering, a different <c>ChangeKind</c>) surfaces as a readable
/// diff. The per-element assertions in <see cref="DefaultSchemaComparerTests"/> stay as the precise spec.
/// </summary>
public sealed class DefaultSchemaComparerSnapshotTests
{
    private readonly DefaultSchemaComparer _sut = new(NullLogger<DefaultSchemaComparer>.Instance);

    [Fact]
    public Task Compare_RichSchemas_ProjectsFullDiffTree()
    {
        // Current: an "app" schema with a users table and a soon-to-be-dropped "scratch" schema.
        var current = DatabaseSchema.Create(
        [
            SchemaDefinition.Create("app", tables:
            [
                Table.Create("users",
                    primaryKey: new PrimaryKey("users_pkey", ["id"]),
                    columns:
                    [
                        Column.Create("id", SqlType.Int),
                        Column.Create("email", SqlType.VarChar(100)),
                        Column.Create("legacy_flag", SqlType.Boolean),
                    ]),
            ]),
            SchemaDefinition.Create("scratch"),
        ]);

        // Desired: id widened, email renamed + retyped, legacy_flag dropped, a new index, a new "reporting" schema.
        var desired = DatabaseSchema.Create(
        [
            SchemaDefinition.Create("app", tables:
            [
                Table.Create("users",
                    primaryKey: new PrimaryKey("users_pkey", ["id"]),
                    columns:
                    [
                        Column.Create("id", SqlType.BigInt),
                        Column.Create("email_address", SqlType.Text, oldName: "email"),
                    ],
                    indexes: [TableIndex.Create("users_email_ix", ["email_address"], isUnique: true)]),
            ]),
            SchemaDefinition.Create("reporting", comment: "analytics"),
        ]);

        return Verify(_sut.Compare(current, desired));
    }
}
