using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Diff;
using NSchema.Schema.Ddl;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.CompositeTypes;
using NSchema.Schema.Model.Constraints;
using NSchema.Schema.Model.Domains;
using NSchema.Schema.Model.Enums;
using NSchema.Schema.Model.Extensions;
using NSchema.Schema.Model.Indexes;
using NSchema.Schema.Model.Routines;
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
        // Current: an "app" schema with a users table, three views, and a soon-to-be-dropped "scratch" schema that
        // carries its own table, view, enum and sequence (so its removal exercises the contained-object drops).
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
                Routines:
                [
                    new Routine("add_tax", RoutineKind.Function, "amount numeric", "RETURNS numeric AS $$ SELECT amount * 1.1 $$"),
                    new Routine("score", RoutineKind.Function, "user_id bigint", "RETURNS numeric AS $$ SELECT 1 $$"),
                    new Routine("stale_fn", RoutineKind.Function, "", "RETURNS int AS $$ SELECT 0 $$"),
                ],
                Domains:
                [
                    new Domain("code", SqlType.Text),
                    new Domain("stale_domain", SqlType.Int),
                ],
                CompositeTypes:
                [
                    new CompositeType("address", [new CompositeField("street", SqlType.Text), new CompositeField("zip", SqlType.Int), new CompositeField("old_field", SqlType.Text)]),
                    new CompositeType("stale_type", [new CompositeField("a", SqlType.Int)]),
                ]),
            new SchemaDefinition("scratch",
                Tables:
                [
                    new Table("temp_data",
                        PrimaryKey: new PrimaryKey("temp_data_pkey", ["id"]),
                        Columns: [new Column("id", SqlType.Int), new Column("payload", SqlType.Text)]),
                ],
                Views: [View("temp_summary", "SELECT count(*) FROM scratch.temp_data")],
                Enums: [new EnumType("temp_status", ["draft"])],
                Sequences: [new Sequence("temp_seq")]),
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
                            new Column("email_upper", SqlType.Text, IsNullable: true, GeneratedExpression: "upper(email_address)"),
                        ],
                        UniqueConstraints: [new UniqueConstraint("users_email_uq", ["email_address"])],
                        CheckConstraints: [new CheckConstraint("users_id_chk", "id > 0")],
                        ExclusionConstraints: [new ExclusionConstraint("users_span_excl",
                            [new ExclusionElement("int4range(0, id)", "&&", IsExpression: true)], Method: "gist")],
                        // A covering, expression, descending index exercising the richer index grammar.
                        Indexes: [new TableIndex("users_email_ix",
                            [new IndexColumn("email_address", Sort: IndexSort.Descending, Nulls: IndexNulls.Last), new IndexColumn("lower(email_address)", IsExpression: true)],
                            IsUnique: true, Method: "btree", Include: ["id"])]),
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
                // Routines: a body replace, a signature change (recreate), an addition, and a procedure.
                Routines:
                [
                    new Routine("add_tax", RoutineKind.Function, "amount numeric", "RETURNS numeric AS $$ SELECT amount * 1.2 $$"),
                    new Routine("score", RoutineKind.Function, "user_id bigint, weight numeric", "RETURNS numeric AS $$ SELECT 1 $$"),
                    new Routine("brand_new", RoutineKind.Function, "", "RETURNS int AS $$ SELECT 42 $$"),
                    new Routine("archive", RoutineKind.Procedure, "before date", "LANGUAGE sql AS $$ DELETE $$"),
                ],
                // Domains: code's base type changes (recreate), stale_domain is dropped, postal_code is added.
                Domains:
                [
                    new Domain("code", SqlType.VarChar(8)),
                    new Domain("postal_code", SqlType.Text, NotNull: true),
                ],
                // Composite types: address retypes a field + adds one + drops one (all in place), stale_type
                // is dropped, and coords is added.
                CompositeTypes:
                [
                    new CompositeType("address", [new CompositeField("street", SqlType.VarChar(120)), new CompositeField("zip", SqlType.Int), new CompositeField("country", SqlType.Text)]),
                    new CompositeType("coords", [new CompositeField("lat", SqlType.Decimal(9, 6)), new CompositeField("lng", SqlType.Decimal(9, 6))]),
                ]),
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
