using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Models;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.CompositeTypes;
using NSchema.Project.Domain.Models.Constraints;
using NSchema.Project.Domain.Models.Domains;
using NSchema.Project.Domain.Models.Enums;
using NSchema.Project.Domain.Models.Extensions;
using NSchema.Project.Domain.Models.Indexes;
using NSchema.Project.Domain.Models.Routines;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Sequences;
using NSchema.Project.Domain.Models.Tables;
using NSchema.Project.Domain.Models.Views;
using NSchema.Project.Nsql;

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
                Tables:
                [
                    new Table(new SqlIdentifier("users"),
                        PrimaryKey: new PrimaryKey(new SqlIdentifier("users_pkey"), [new SqlIdentifier("id")]),
                        Columns:
                        [
                            new Column(new SqlIdentifier("id"), SqlType.Int),
                            new Column(new SqlIdentifier("email"), SqlType.VarChar(100)),
                            new Column(new SqlIdentifier("legacy_flag"), SqlType.Boolean),
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
                    new EnumType(new SqlIdentifier("order_status"), ["pending", "shipped"]),
                    new EnumType(new SqlIdentifier("importance"), ["low", "high"]),
                    new EnumType(new SqlIdentifier("stale_enum"), ["x"]),
                ],
                Sequences:
                [
                    new Sequence(new SqlIdentifier("order_id"), new SequenceOptions(StartWith: 1, IncrementBy: 1)),
                    new Sequence(new SqlIdentifier("stale_seq")),
                ],
                Routines:
                [
                    new Routine(new SqlIdentifier("add_tax"), RoutineKind.Function, new SqlText("amount numeric"), new SqlText("RETURNS numeric AS $$ SELECT amount * 1.1 $$")),
                    new Routine(new SqlIdentifier("score"), RoutineKind.Function, new SqlText("user_id bigint"), new SqlText("RETURNS numeric AS $$ SELECT 1 $$")),
                    new Routine(new SqlIdentifier("stale_fn"), RoutineKind.Function, new SqlText(""), new SqlText("RETURNS int AS $$ SELECT 0 $$")),
                ],
                Domains:
                [
                    new DomainType(new SqlIdentifier("code"), SqlType.Text),
                    new DomainType(new SqlIdentifier("stale_domain"), SqlType.Int),
                ],
                CompositeTypes:
                [
                    new CompositeType(new SqlIdentifier("address"), [new CompositeField(new SqlIdentifier("street"), SqlType.Text), new CompositeField(new SqlIdentifier("zip"), SqlType.Int), new CompositeField(new SqlIdentifier("old_field"), SqlType.Text)]),
                    new CompositeType(new SqlIdentifier("stale_type"), [new CompositeField(new SqlIdentifier("a"), SqlType.Int)]),
                ]),
            new Schema(new SqlIdentifier("scratch"),
                Tables:
                [
                    new Table(new SqlIdentifier("temp_data"),
                        PrimaryKey: new PrimaryKey(new SqlIdentifier("temp_data_pkey"), [new SqlIdentifier("id")]),
                        Columns: [new Column(new SqlIdentifier("id"), SqlType.Int), new Column(new SqlIdentifier("payload"), SqlType.Text)]),
                ],
                Views: [View("temp_summary", "SELECT count(*) FROM scratch.temp_data")],
                Enums: [new EnumType(new SqlIdentifier("temp_status"), ["draft"])],
                Sequences: [new Sequence(new SqlIdentifier("temp_seq"))]),
        ],
        Extensions:
        [
            new Extension(new SqlIdentifier("citext")),
            new Extension(new SqlIdentifier("postgis"), Version: "3.3"),
            new Extension(new SqlIdentifier("legacy_ext")),
        ]);

        // Desired: id widened, email renamed + retyped, legacy_flag dropped, a new index, a new unique
        // constraint, a new check constraint, and a new "reporting" schema. Views: active_users' body changes
        // (a replace), legacy_report is renamed to report, old_summary is dropped, and user_emails is added
        // (reading another view, so it carries a dependency).
        var desired = new Database(
        [
            new Schema(new SqlIdentifier("app"),
                Tables:
                [
                    new Table(new SqlIdentifier("users"),
                        PrimaryKey: new PrimaryKey(new SqlIdentifier("users_pkey"), [new SqlIdentifier("id")]),
                        Columns:
                        [
                            new Column(new SqlIdentifier("id"), SqlType.BigInt),
                            new Column(new SqlIdentifier("email_address"), SqlType.Text),
                            new Column(new SqlIdentifier("email_upper"), SqlType.Text, IsNullable: true, GeneratedExpression: new SqlText("upper(email_address)")),
                        ],
                        UniqueConstraints: [new UniqueConstraint(new SqlIdentifier("users_email_uq"), [new SqlIdentifier("email_address")])],
                        CheckConstraints: [new CheckConstraint(new SqlIdentifier("users_id_chk"), new SqlText("id > 0"))],
                        ExclusionConstraints: [new ExclusionConstraint(new SqlIdentifier("users_span_excl"),
                            [new ExclusionElement("&&", Expression: new SqlText("int4range(0, id)"))], Method: "gist")],
                        // A covering, expression, descending index exercising the richer index grammar.
                        Indexes: [new TableIndex(new SqlIdentifier("users_email_ix"),
                            [new IndexColumn(new SqlIdentifier("email_address"), Sort: IndexSort.Descending, Nulls: IndexNulls.Last), new IndexColumn(Expression: new SqlText("lower(email_address)"))],
                            IsUnique: true, Method: "btree", Include: [new SqlIdentifier("id")])]),
                ],
                Views:
                [
                    View("active_users", "SELECT id, email_address FROM app.users WHERE active"),
                    View("report", "SELECT * FROM app.users"),
                    View("user_emails", "SELECT email_address FROM app.active_users"),
                ],
                // Enums: a value appended, a rename, a drop, and an addition.
                Enums:
                [
                    new EnumType(new SqlIdentifier("order_status"), ["pending", "shipped", "delivered"]),
                    new EnumType(new SqlIdentifier("priority"), ["low", "high"]),
                    new EnumType(new SqlIdentifier("severity"), ["info", "error"]),
                ],
                // Sequences: an options change, a drop, and an addition.
                Sequences:
                [
                    new Sequence(new SqlIdentifier("order_id"), new SequenceOptions(StartWith: 1000, IncrementBy: 10, Cycle: true)),
                    new Sequence(new SqlIdentifier("batch_id")),
                ],
                // Routines: a body replace, a signature change (recreate), an addition, and a procedure.
                Routines:
                [
                    new Routine(new SqlIdentifier("add_tax"), RoutineKind.Function, new SqlText("amount numeric"), new SqlText("RETURNS numeric AS $$ SELECT amount * 1.2 $$")),
                    new Routine(new SqlIdentifier("score"), RoutineKind.Function, new SqlText("user_id bigint, weight numeric"), new SqlText("RETURNS numeric AS $$ SELECT 1 $$")),
                    new Routine(new SqlIdentifier("brand_new"), RoutineKind.Function, new SqlText(""), new SqlText("RETURNS int AS $$ SELECT 42 $$")),
                    new Routine(new SqlIdentifier("archive"), RoutineKind.Procedure, new SqlText("before date"), new SqlText("LANGUAGE sql AS $$ DELETE $$")),
                ],
                // Domains: code's base type changes (recreate), stale_domain is dropped, postal_code is added.
                Domains:
                [
                    new DomainType(new SqlIdentifier("code"), SqlType.VarChar(8)),
                    new DomainType(new SqlIdentifier("postal_code"), SqlType.Text, NotNull: true),
                ],
                // Composite types: address retypes a field + adds one + drops one (all in place), stale_type
                // is dropped, and coords is added.
                CompositeTypes:
                [
                    new CompositeType(new SqlIdentifier("address"), [new CompositeField(new SqlIdentifier("street"), SqlType.VarChar(120)), new CompositeField(new SqlIdentifier("zip"), SqlType.Int), new CompositeField(new SqlIdentifier("country"), SqlType.Text)]),
                    new CompositeType(new SqlIdentifier("coords"), [new CompositeField(new SqlIdentifier("lat"), SqlType.Decimal(9, 6)), new CompositeField(new SqlIdentifier("lng"), SqlType.Decimal(9, 6))]),
                ]),
            new Schema(new SqlIdentifier("reporting"), Comment: "analytics"),
        ],
        // Extensions: citext unchanged, postgis version bump, legacy_ext dropped, vector added.
        Extensions:
        [
            new Extension(new SqlIdentifier("citext")),
            new Extension(new SqlIdentifier("postgis"), Version: "3.4"),
            new Extension(new SqlIdentifier("vector"), Comment: "embeddings"),
        ]);

        // The renames and drops the comment narrates arrive as directives, addressing current reality.
        var directives = new ProjectDirectives(
            Tables: new NSchema.Project.Domain.Models.Tables.TableDirectives(ColumnRenames:
                [new MemberRename(new MemberReference(new SqlIdentifier("app"), new SqlIdentifier("users"), new SqlIdentifier("email")), new SqlIdentifier("email_address"))]),
            Views: new NSchema.Project.Domain.Models.Views.ViewDirectives(Renames:
                [new ObjectRename(new ObjectReference(new SqlIdentifier("app"), new SqlIdentifier("legacy_report")), new SqlIdentifier("report"))]),
            Enums: new NSchema.Project.Domain.Models.Enums.EnumDirectives(Renames:
                [new ObjectRename(new ObjectReference(new SqlIdentifier("app"), new SqlIdentifier("importance")), new SqlIdentifier("priority"))]),
            Extensions: new NSchema.Project.Domain.Models.Extensions.ExtensionDirectives(Drops: [new SqlIdentifier("legacy_ext")]));

        return Verify(Compare(current, desired, directives));
    }

    // Builds a view with its dependencies derived from the body, exactly as the DDL parser would.
    private static View View(string name, string body) =>
        new(new SqlIdentifier(name), new SqlText(body), null, ViewDependencyExtractor.Extract(body, new SqlIdentifier("app")));

    private DatabaseDiff Compare(Database current, Database desired, ProjectDirectives? directives = null) =>
        _sut.Compare(current, desired, directives ?? ProjectDirectives.Empty);
}
