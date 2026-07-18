using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Diff.Model;
using NSchema.Diff.Model.Services;
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
        var current = new Database
        {
            Schemas = [
            new Schema { Name = new SqlIdentifier("app"),
                Tables = [
                    new Table { Name = new SqlIdentifier("users"),
                        PrimaryKey = new PrimaryKey { Name = new SqlIdentifier("users_pkey"), ColumnNames = [new SqlIdentifier("id")] },
                        Columns = [
                            new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int },
                            new Column { Name = new SqlIdentifier("email"), Type = SqlType.VarChar(100) },
                            new Column { Name = new SqlIdentifier("legacy_flag"), Type = SqlType.Boolean },
                        ] },
                ],
                Views = [
                    View("active_users", "SELECT id FROM app.users WHERE active"),
                    View("legacy_report", "SELECT * FROM app.users"),
                    View("old_summary", "SELECT count(*) FROM app.users"),
                ],
                Enums = [
                    new EnumType { Name = new SqlIdentifier("order_status"), Values = ["pending", "shipped"] },
                    new EnumType { Name = new SqlIdentifier("importance"), Values = ["low", "high"] },
                    new EnumType { Name = new SqlIdentifier("stale_enum"), Values = ["x"] },
                ],
                Sequences = [
                    new Sequence { Name = new SqlIdentifier("order_id"), Options = new SequenceOptions(StartWith: 1, IncrementBy: 1) },
                    new Sequence { Name = new SqlIdentifier("stale_seq") },
                ],
                Routines = [
                    new Routine { Name = new SqlIdentifier("add_tax"), RoutineKind = RoutineKind.Function, Arguments = new SqlText("amount numeric"), Definition = new SqlText("RETURNS numeric AS $$ SELECT amount * 1.1 $$") },
                    new Routine { Name = new SqlIdentifier("score"), RoutineKind = RoutineKind.Function, Arguments = new SqlText("user_id bigint"), Definition = new SqlText("RETURNS numeric AS $$ SELECT 1 $$") },
                    new Routine { Name = new SqlIdentifier("stale_fn"), RoutineKind = RoutineKind.Function, Arguments = new SqlText(""), Definition = new SqlText("RETURNS int AS $$ SELECT 0 $$") },
                ],
                Domains = [
                    new DomainType { Name = new SqlIdentifier("code"), DataType = SqlType.Text },
                    new DomainType { Name = new SqlIdentifier("stale_domain"), DataType = SqlType.Int },
                ],
                CompositeTypes = [
                    new CompositeType { Name = new SqlIdentifier("address"), Fields = [new CompositeField(new SqlIdentifier("street"), SqlType.Text), new CompositeField(new SqlIdentifier("zip"), SqlType.Int), new CompositeField(new SqlIdentifier("old_field"), SqlType.Text)] },
                    new CompositeType { Name = new SqlIdentifier("stale_type"), Fields = [new CompositeField(new SqlIdentifier("a"), SqlType.Int)] },
                ] },
            new Schema { Name = new SqlIdentifier("scratch"),
                Tables = [
                    new Table { Name = new SqlIdentifier("temp_data"),
                        PrimaryKey = new PrimaryKey { Name = new SqlIdentifier("temp_data_pkey"), ColumnNames = [new SqlIdentifier("id")] },
                        Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int }, new Column { Name = new SqlIdentifier("payload"), Type = SqlType.Text }] },
                ],
                Views = [View("temp_summary", "SELECT count(*) FROM scratch.temp_data")],
                Enums = [new EnumType { Name = new SqlIdentifier("temp_status"), Values = ["draft"] }],
                Sequences = [new Sequence { Name = new SqlIdentifier("temp_seq") }] },
        ],
            Extensions = [
            new Extension { Name = new SqlIdentifier("citext") },
            new Extension { Name = new SqlIdentifier("postgis"), Version = "3.3" },
            new Extension { Name = new SqlIdentifier("legacy_ext") },
        ],
        };

        // Desired: id widened, email renamed + retyped, legacy_flag dropped, a new index, a new unique
        // constraint, a new check constraint, and a new "reporting" schema. Views: active_users' body changes
        // (a replace), legacy_report is renamed to report, old_summary is dropped, and user_emails is added
        // (reading another view, so it carries a dependency).
        var desired = new Database
        {
            Schemas = [
            new Schema { Name = new SqlIdentifier("app"),
                Tables = [
                    new Table { Name = new SqlIdentifier("users"),
                        PrimaryKey = new PrimaryKey { Name = new SqlIdentifier("users_pkey"), ColumnNames = [new SqlIdentifier("id")] },
                        Columns = [
                            new Column { Name = new SqlIdentifier("id"), Type = SqlType.BigInt },
                            new Column { Name = new SqlIdentifier("email_address"), Type = SqlType.Text },
                            new Column { Name = new SqlIdentifier("email_upper"), Type = SqlType.Text, IsNullable = true, GeneratedExpression = new SqlText("upper(email_address)") },
                        ],
                        UniqueConstraints = [new UniqueConstraint { Name = new SqlIdentifier("users_email_uq"), ColumnNames = [new SqlIdentifier("email_address")] }],
                        CheckConstraints = [new CheckConstraint { Name = new SqlIdentifier("users_id_chk"), Expression = new SqlText("id > 0") }],
                        ExclusionConstraints = [new ExclusionConstraint { Name = new SqlIdentifier("users_span_excl"),
                            Elements = [new ExclusionElement("&&", Expression: new SqlText("int4range(0, id)"))], Method = "gist" }],
                        // A covering, expression, descending index exercising the richer index grammar.
                        Indexes = [new TableIndex { Name = new SqlIdentifier("users_email_ix"),
                            Columns = [new IndexColumn(new SqlIdentifier("email_address"), Sort: IndexSort.Descending, Nulls: IndexNulls.Last), new IndexColumn(Expression: new SqlText("lower(email_address)"))],
                            IsUnique = true, Method = "btree", Include = [new SqlIdentifier("id")] }] },
                ],
                Views = [
                    View("active_users", "SELECT id, email_address FROM app.users WHERE active"),
                    View("report", "SELECT * FROM app.users"),
                    View("user_emails", "SELECT email_address FROM app.active_users"),
                ],
                // Enums: a value appended, a rename, a drop, and an addition.
                Enums = [
                    new EnumType { Name = new SqlIdentifier("order_status"), Values = ["pending", "shipped", "delivered"] },
                    new EnumType { Name = new SqlIdentifier("priority"), Values = ["low", "high"] },
                    new EnumType { Name = new SqlIdentifier("severity"), Values = ["info", "error"] },
                ],
                // Sequences: an options change, a drop, and an addition.
                Sequences = [
                    new Sequence { Name = new SqlIdentifier("order_id"), Options = new SequenceOptions(StartWith: 1000, IncrementBy: 10, Cycle: true) },
                    new Sequence { Name = new SqlIdentifier("batch_id") },
                ],
                // Routines: a body replace, a signature change (recreate), an addition, and a procedure.
                Routines = [
                    new Routine { Name = new SqlIdentifier("add_tax"), RoutineKind = RoutineKind.Function, Arguments = new SqlText("amount numeric"), Definition = new SqlText("RETURNS numeric AS $$ SELECT amount * 1.2 $$") },
                    new Routine { Name = new SqlIdentifier("score"), RoutineKind = RoutineKind.Function, Arguments = new SqlText("user_id bigint, weight numeric"), Definition = new SqlText("RETURNS numeric AS $$ SELECT 1 $$") },
                    new Routine { Name = new SqlIdentifier("brand_new"), RoutineKind = RoutineKind.Function, Arguments = new SqlText(""), Definition = new SqlText("RETURNS int AS $$ SELECT 42 $$") },
                    new Routine { Name = new SqlIdentifier("archive"), RoutineKind = RoutineKind.Procedure, Arguments = new SqlText("before date"), Definition = new SqlText("LANGUAGE sql AS $$ DELETE $$") },
                ],
                // Domains: code's base type changes (recreate), stale_domain is dropped, postal_code is added.
                Domains = [
                    new DomainType { Name = new SqlIdentifier("code"), DataType = SqlType.VarChar(8) },
                    new DomainType { Name = new SqlIdentifier("postal_code"), DataType = SqlType.Text, NotNull = true },
                ],
                // Composite types: address retypes a field + adds one + drops one (all in place), stale_type
                // is dropped, and coords is added.
                CompositeTypes = [
                    new CompositeType { Name = new SqlIdentifier("address"), Fields = [new CompositeField(new SqlIdentifier("street"), SqlType.VarChar(120)), new CompositeField(new SqlIdentifier("zip"), SqlType.Int), new CompositeField(new SqlIdentifier("country"), SqlType.Text)] },
                    new CompositeType { Name = new SqlIdentifier("coords"), Fields = [new CompositeField(new SqlIdentifier("lat"), SqlType.Decimal(9, 6)), new CompositeField(new SqlIdentifier("lng"), SqlType.Decimal(9, 6))] },
                ] },
            new Schema { Name = new SqlIdentifier("reporting"), Comment = "analytics" },
        ],
            // Extensions: citext unchanged, postgis version bump, legacy_ext dropped, vector added.
            Extensions = [
            new Extension { Name = new SqlIdentifier("citext") },
            new Extension { Name = new SqlIdentifier("postgis"), Version = "3.4" },
            new Extension { Name = new SqlIdentifier("vector"), Comment = "embeddings" },
        ],
        };

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
        new View { Name = new SqlIdentifier(name), Body = new SqlText(body), DependsOn = ViewDependencyExtractor.Extract(body, new SqlIdentifier("app")) };

    private DatabaseDiff Compare(Database current, Database desired, ProjectDirectives? directives = null)
    {
        var effective = directives ?? ProjectDirectives.Empty;
        var aligned = DatabaseAligner.Align(current, desired, effective);
        return _sut.Compare(aligned.Require(), desired);
    }
}
