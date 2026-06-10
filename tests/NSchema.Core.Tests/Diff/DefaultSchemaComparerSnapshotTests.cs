using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Diff;
using NSchema.Schema.Model;
using NSchema.Schema.Serialization.Ddl;

namespace NSchema.Tests.Diff;

/// <summary>
/// Snapshot coverage for <see cref="SchemaComparer"/>. Demonstrates Verify diffing a complex
/// object graph: the comparer's whole <c>MigrationDiff</c> tree is serialized and pinned, so a change
/// to the projection (a new field, a reordering, a different <c>ChangeKind</c>) surfaces as a readable
/// diff. The per-element assertions in <see cref="SchemaComparerTests"/> stay as the precise spec.
/// </summary>
public sealed class DefaultSchemaComparerSnapshotTests
{
    private readonly SchemaComparer _sut = new(NullLogger<SchemaComparer>.Instance);

    [Fact]
    public Task Compare_RichSchemas_ProjectsFullDiffTree()
    {
        // Current: an "app" schema with a users table, three views, and a soon-to-be-dropped "scratch" schema.
        var current = new DatabaseSchema(
        [
            new SchemaDefinition("app",
                Tables:
                [
                    new Table("users",
                        PrimaryKey: new PrimaryKey("users_pkey", ["id"]),
                        Columns:
                        [
                            new Column("id", SqlType.Int),
                            new Column("email", SqlType.VarChar(100)),
                            new Column("legacy_flag", SqlType.Boolean),
                        ]),
                ],
                Views:
                [
                    View("active_users", "SELECT id FROM app.users WHERE active"),
                    View("legacy_report", "SELECT * FROM app.users"),
                    View("old_summary", "SELECT count(*) FROM app.users"),
                ]),
            new SchemaDefinition("scratch"),
        ]);

        // Desired: id widened, email renamed + retyped, legacy_flag dropped, a new index, a new unique
        // constraint, a new check constraint, and a new "reporting" schema. Views: active_users' body changes
        // (a replace), legacy_report is renamed to report, old_summary is dropped, and user_emails is added
        // (reading another view, so it carries a dependency).
        var desired = new DatabaseSchema(
        [
            new SchemaDefinition("app",
                Tables:
                [
                    new Table("users",
                        PrimaryKey: new PrimaryKey("users_pkey", ["id"]),
                        Columns:
                        [
                            new Column("id", SqlType.BigInt),
                            new Column("email_address", SqlType.Text, OldName: "email"),
                        ],
                        UniqueConstraints: [new UniqueConstraint("users_email_uq", ["email_address"])],
                        CheckConstraints: [new CheckConstraint("users_id_chk", "id > 0")],
                        Indexes: [new TableIndex("users_email_ix", ["email_address"], IsUnique: true)]),
                ],
                Views:
                [
                    View("active_users", "SELECT id, email_address FROM app.users WHERE active"),
                    View("report", "SELECT * FROM app.users", oldName: "legacy_report"),
                    View("user_emails", "SELECT email_address FROM app.active_users"),
                ]),
            new SchemaDefinition("reporting", Comment: "analytics"),
        ]);

        return Verify(_sut.Compare(current, desired));
    }

    // Builds a view with its dependencies derived from the body, exactly as the DSL parser would.
    private static View View(string name, string body, string? oldName = null) =>
        new(name, body, oldName, null, ViewDependencyExtractor.Extract(body, "app"));
}
