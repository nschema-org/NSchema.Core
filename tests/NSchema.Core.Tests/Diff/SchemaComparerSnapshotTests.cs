using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Diff;
using NSchema.Schema.Ddl;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Constraints;
using NSchema.Schema.Model.Enums;
using NSchema.Schema.Model.Extensions;
using NSchema.Schema.Model.Functions;
using NSchema.Schema.Model.Indexes;
using NSchema.Schema.Model.Procedures;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Sequences;
using NSchema.Schema.Model.Tables;
using NSchema.Schema.Model.Views;

namespace NSchema.Tests.Diff;

/// <summary>
/// Snapshot coverage for <see cref="SchemaComparer"/>. Demonstrates Verify diffing a complex
/// object graph: the comparer's whole <c>MigrationDiff</c> tree is serialized and pinned, so a change
/// to the projection (a new field, a reordering, a different <c>ChangeKind</c>) surfaces as a readable
/// diff. The per-element assertions in <see cref="SchemaComparerTests"/> stay as the precise spec.
/// </summary>
public sealed class SchemaComparerSnapshotTests
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
                ],
                Enums:
                [
                    new EnumType("order_status", ["pending", "shipped"]),
                    new EnumType("importance", ["low", "high"]),
                    new EnumType("stale_enum", ["x"]),
                ],
                Sequences:
                [
                    new Sequence("order_id", new SequenceOptions(StartWith: 1, IncrementBy: 1)),
                    new Sequence("stale_seq"),
                ],
                Functions:
                [
                    new Function("add_tax", "amount numeric", "RETURNS numeric AS $$ SELECT amount * 1.1 $$"),
                    new Function("score", "user_id bigint", "RETURNS numeric AS $$ SELECT 1 $$"),
                    new Function("stale_fn", "", "RETURNS int AS $$ SELECT 0 $$"),
                ]),
            new SchemaDefinition("scratch"),
        ],
        Extensions:
        [
            new Extension("citext"),
            new Extension("postgis", Version: "3.3"),
            new Extension("legacy_ext"),
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
                ],
                // Enums: a value appended, a rename, a drop, and an addition.
                Enums:
                [
                    new EnumType("order_status", ["pending", "shipped", "delivered"]),
                    new EnumType("priority", ["low", "high"], OldName: "importance"),
                    new EnumType("severity", ["info", "error"]),
                ],
                // Sequences: an options change, a drop, and an addition.
                Sequences:
                [
                    new Sequence("order_id", new SequenceOptions(StartWith: 1000, IncrementBy: 10, Cycle: true)),
                    new Sequence("batch_id"),
                ],
                // Functions: a body replace, a signature change (recreate), and an addition.
                Functions:
                [
                    new Function("add_tax", "amount numeric", "RETURNS numeric AS $$ SELECT amount * 1.2 $$"),
                    new Function("score", "user_id bigint, weight numeric", "RETURNS numeric AS $$ SELECT 1 $$"),
                    new Function("brand_new", "", "RETURNS int AS $$ SELECT 42 $$"),
                ],
                Procedures: [new Procedure("archive", "before date", "LANGUAGE sql AS $$ DELETE $$")]),
            new SchemaDefinition("reporting", Comment: "analytics"),
        ],
        // Extensions: citext unchanged, postgis version bump, legacy_ext dropped, vector added.
        Extensions:
        [
            new Extension("citext"),
            new Extension("postgis", Version: "3.4"),
            new Extension("vector", Comment: "embeddings"),
        ],
        DroppedExtensions: ["legacy_ext"]);

        return Verify(_sut.Compare(current, desired));
    }

    // Builds a view with its dependencies derived from the body, exactly as the DDL parser would.
    private static View View(string name, string body, string? oldName = null) =>
        new(name, body, oldName, null, ViewDependencyExtractor.Extract(body, "app"));
}
