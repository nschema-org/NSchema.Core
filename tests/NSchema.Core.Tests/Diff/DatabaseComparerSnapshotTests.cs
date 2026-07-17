using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Diff.Model;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Constraints;
using NSchema.Model.Domains;
using NSchema.Model.Enums;
using NSchema.Model.Extensions;
using NSchema.Model.Indexes;
using NSchema.Model.Routines;
using NSchema.Model.Schemas;
using NSchema.Model.Sequences;
using NSchema.Model.Tables;
using NSchema.Model.Views;
using NSchema.Project.Model.Directives;
using NSchema.Project.Nsql;
using DatabaseComparer = NSchema.Diff.Model.Services.DatabaseComparer;

namespace NSchema.Tests.Diff;

/// <summary>
/// Snapshot coverage for <see cref="DatabaseComparer"/>. Demonstrates Verify diffing a complex
/// object graph: the comparer's whole <c>MigrationDiff</c> tree is serialized and pinned, so a change
/// to the projection (a new field, a reordering, a different <c>ChangeKind</c>) surfaces as a readable
/// diff. The per-element assertions in <see cref="DatabaseComparerTests"/> stay as the precise spec.
/// </summary>
public sealed class DatabaseComparerSnapshotTests
{
    private readonly DatabaseComparer _sut = new(NullLogger<DatabaseComparer>.Instance);

    [Fact]
    public Task Compare_RichSchemas_ProjectsFullDiffTree()
    {
        // Current: an "app" schema with a users table, three views, and a soon-to-be-dropped "scratch" schema that
        // carries its own table, view, enum and sequence (so its removal exercises the contained-object drops).
        var current = new Database(
        [
            new Schema(new SqlIdentifier("app"),
                tables:
                [
                    new Table(new SqlIdentifier("users"),
                        primaryKey: new PrimaryKey(new SqlIdentifier("users_pkey"), [new SqlIdentifier("id")]),
                        columns:
                        [
                            new Column(new SqlIdentifier("id"), SqlType.Int),
                            new Column(new SqlIdentifier("email"), SqlType.VarChar(100)),
                            new Column(new SqlIdentifier("legacy_flag"), SqlType.Boolean),
                        ]),
                ],
                views:
                [
                    View("active_users", "SELECT id FROM app.users WHERE active"),
                    View("legacy_report", "SELECT * FROM app.users"),
                    View("old_summary", "SELECT count(*) FROM app.users"),
                ],
                enums:
                [
                    new EnumType(new SqlIdentifier("order_status"), ["pending", "shipped"]),
                    new EnumType(new SqlIdentifier("importance"), ["low", "high"]),
                    new EnumType(new SqlIdentifier("stale_enum"), ["x"]),
                ],
                sequences:
                [
                    new Sequence(new SqlIdentifier("order_id"), new SequenceOptions(StartWith: 1, IncrementBy: 1)),
                    new Sequence(new SqlIdentifier("stale_seq")),
                ],
                routines:
                [
                    new Routine(new SqlIdentifier("add_tax"), RoutineKind.Function, new SqlText("amount numeric"), new SqlText("RETURNS numeric AS $$ SELECT amount * 1.1 $$")),
                    new Routine(new SqlIdentifier("score"), RoutineKind.Function, new SqlText("user_id bigint"), new SqlText("RETURNS numeric AS $$ SELECT 1 $$")),
                    new Routine(new SqlIdentifier("stale_fn"), RoutineKind.Function, new SqlText(""), new SqlText("RETURNS int AS $$ SELECT 0 $$")),
                ],
                domains:
                [
                    new DomainType(new SqlIdentifier("code"), SqlType.Text),
                    new DomainType(new SqlIdentifier("stale_domain"), SqlType.Int),
                ],
                compositeTypes:
                [
                    new CompositeType(new SqlIdentifier("address"), [new CompositeField(new SqlIdentifier("street"), SqlType.Text), new CompositeField(new SqlIdentifier("zip"), SqlType.Int), new CompositeField(new SqlIdentifier("old_field"), SqlType.Text)]),
                    new CompositeType(new SqlIdentifier("stale_type"), [new CompositeField(new SqlIdentifier("a"), SqlType.Int)]),
                ]),
            new Schema(new SqlIdentifier("scratch"),
                tables:
                [
                    new Table(new SqlIdentifier("temp_data"),
                        primaryKey: new PrimaryKey(new SqlIdentifier("temp_data_pkey"), [new SqlIdentifier("id")]),
                        columns: [new Column(new SqlIdentifier("id"), SqlType.Int), new Column(new SqlIdentifier("payload"), SqlType.Text)]),
                ],
                views: [View("temp_summary", "SELECT count(*) FROM scratch.temp_data")],
                enums: [new EnumType(new SqlIdentifier("temp_status"), ["draft"])],
                sequences: [new Sequence(new SqlIdentifier("temp_seq"))]),
        ],
        extensions:
        [
            new Extension(new SqlIdentifier("citext")),
            new Extension(new SqlIdentifier("postgis"), version: "3.3"),
            new Extension(new SqlIdentifier("legacy_ext")),
        ]);

        // Desired: id widened, email renamed + retyped, legacy_flag dropped, a new index, a new unique
        // constraint, a new check constraint, and a new "reporting" schema. Views: active_users' body changes
        // (a replace), legacy_report is renamed to report, old_summary is dropped, and user_emails is added
        // (reading another view, so it carries a dependency).
        var desired = new Database(
        [
            new Schema(new SqlIdentifier("app"),
                tables:
                [
                    new Table(new SqlIdentifier("users"),
                        primaryKey: new PrimaryKey(new SqlIdentifier("users_pkey"), [new SqlIdentifier("id")]),
                        columns:
                        [
                            new Column(new SqlIdentifier("id"), SqlType.BigInt),
                            new Column(new SqlIdentifier("email_address"), SqlType.Text),
                            new Column(new SqlIdentifier("email_upper"), SqlType.Text, isNullable: true, generatedExpression: new SqlText("upper(email_address)")),
                        ],
                        uniqueConstraints: [new UniqueConstraint(new SqlIdentifier("users_email_uq"), [new SqlIdentifier("email_address")])],
                        checkConstraints: [new CheckConstraint(new SqlIdentifier("users_id_chk"), new SqlText("id > 0"))],
                        exclusionConstraints: [new ExclusionConstraint(new SqlIdentifier("users_span_excl"),
                            [new ExclusionElement("&&", Expression: new SqlText("int4range(0, id)"))], method: "gist")],
                        // A covering, expression, descending index exercising the richer index grammar.
                        indexes: [new TableIndex(new SqlIdentifier("users_email_ix"),
                            [new IndexColumn(new SqlIdentifier("email_address"), Sort: IndexSort.Descending, Nulls: IndexNulls.Last), new IndexColumn(Expression: new SqlText("lower(email_address)"))],
                            isUnique: true, method: "btree", include: [new SqlIdentifier("id")])]),
                ],
                views:
                [
                    View("active_users", "SELECT id, email_address FROM app.users WHERE active"),
                    View("report", "SELECT * FROM app.users"),
                    View("user_emails", "SELECT email_address FROM app.active_users"),
                ],
                // Enums: a value appended, a rename, a drop, and an addition.
                enums:
                [
                    new EnumType(new SqlIdentifier("order_status"), ["pending", "shipped", "delivered"]),
                    new EnumType(new SqlIdentifier("priority"), ["low", "high"]),
                    new EnumType(new SqlIdentifier("severity"), ["info", "error"]),
                ],
                // Sequences: an options change, a drop, and an addition.
                sequences:
                [
                    new Sequence(new SqlIdentifier("order_id"), new SequenceOptions(StartWith: 1000, IncrementBy: 10, Cycle: true)),
                    new Sequence(new SqlIdentifier("batch_id")),
                ],
                // Routines: a body replace, a signature change (recreate), an addition, and a procedure.
                routines:
                [
                    new Routine(new SqlIdentifier("add_tax"), RoutineKind.Function, new SqlText("amount numeric"), new SqlText("RETURNS numeric AS $$ SELECT amount * 1.2 $$")),
                    new Routine(new SqlIdentifier("score"), RoutineKind.Function, new SqlText("user_id bigint, weight numeric"), new SqlText("RETURNS numeric AS $$ SELECT 1 $$")),
                    new Routine(new SqlIdentifier("brand_new"), RoutineKind.Function, new SqlText(""), new SqlText("RETURNS int AS $$ SELECT 42 $$")),
                    new Routine(new SqlIdentifier("archive"), RoutineKind.Procedure, new SqlText("before date"), new SqlText("LANGUAGE sql AS $$ DELETE $$")),
                ],
                // Domains: code's base type changes (recreate), stale_domain is dropped, postal_code is added.
                domains:
                [
                    new DomainType(new SqlIdentifier("code"), SqlType.VarChar(8)),
                    new DomainType(new SqlIdentifier("postal_code"), SqlType.Text, notNull: true),
                ],
                // Composite types: address retypes a field + adds one + drops one (all in place), stale_type
                // is dropped, and coords is added.
                compositeTypes:
                [
                    new CompositeType(new SqlIdentifier("address"), [new CompositeField(new SqlIdentifier("street"), SqlType.VarChar(120)), new CompositeField(new SqlIdentifier("zip"), SqlType.Int), new CompositeField(new SqlIdentifier("country"), SqlType.Text)]),
                    new CompositeType(new SqlIdentifier("coords"), [new CompositeField(new SqlIdentifier("lat"), SqlType.Decimal(9, 6)), new CompositeField(new SqlIdentifier("lng"), SqlType.Decimal(9, 6))]),
                ]),
            new Schema(new SqlIdentifier("reporting")) { Comment = "analytics" },
        ],
        // Extensions: citext unchanged, postgis version bump, legacy_ext dropped, vector added.
        extensions:
        [
            new Extension(new SqlIdentifier("citext")),
            new Extension(new SqlIdentifier("postgis"), version: "3.4"),
            new Extension(new SqlIdentifier("vector")) { Comment = "embeddings" },
        ]);

        // The renames and drops the comment narrates arrive as directives, addressing current reality.
        var directives = new ProjectDirectives(
            MemberRenames: [new MemberRenameDirective(new MemberAddress(new SqlIdentifier("app"), new SqlIdentifier("users"), new SqlIdentifier("email")), new SqlIdentifier("email_address"))],
            ObjectRenames:
            [
                new ObjectRenameDirective(new ObjectIdentity(ObjectKind.View, new ObjectAddress(new SqlIdentifier("app"), new SqlIdentifier("legacy_report"))), new SqlIdentifier("report")),
                new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Enum, new ObjectAddress(new SqlIdentifier("app"), new SqlIdentifier("importance"))), new SqlIdentifier("priority")),
            ]);

        return Verify(Compare(current, desired, directives));
    }

    // Builds a view with its dependencies derived from the body, exactly as the DDL parser would.
    private static View View(string name, string body) =>
        new(new SqlIdentifier(name), new SqlText(body), ViewDependencyExtractor.Extract(body, new SqlIdentifier("app")));

    private DatabaseDiff Compare(Database current, Database desired, ProjectDirectives? directives = null) =>
        _sut.Compare(current, desired, directives ?? ProjectDirectives.Empty);
}
