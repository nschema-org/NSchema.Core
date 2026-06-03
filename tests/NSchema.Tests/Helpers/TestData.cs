using NSchema.Migration.Diff.Model;
using NSchema.Migration.Plan;
using NSchema.Schema;

namespace NSchema.Tests.Helpers;

public static class TestData
{
    public static readonly MigrationAction DestructiveAction = new DropTable("identity", "users");
    public static readonly MigrationAction NonDestructiveAction = new CreateSchema("identity");

    public static readonly DatabaseSchema EmptySchema = DatabaseSchema.Create([]);

    public static readonly MigrationPlan EmptyPlan = new([], EmptySchema);
    public static readonly MigrationPlan DestructivePlan = new([DestructiveAction], EmptySchema);
    public static readonly MigrationPlan NonDestructivePlan = new([NonDestructiveAction], EmptySchema);

    /// <summary>A diff dropping the <c>identity.users</c> table.</summary>
    public static readonly MigrationDiff DestructiveDiff = DiffWithDroppedTables("users");

    /// <summary>A diff that only adds a schema.</summary>
    public static readonly MigrationDiff NonDestructiveDiff = new(
        [new SchemaDiff("identity", ChangeKind.Add, null, null, [], [])], [], []);

    /// <summary>Builds a diff that drops the named tables from the <c>identity</c> schema.</summary>
    public static MigrationDiff DiffWithDroppedTables(params string[] tableNames) => new(
        [new SchemaDiff("identity", null, null, null, [],
            [.. tableNames.Select(name => new TableDiff("identity", name, ChangeKind.Remove, null, null, [], [], [], []))])],
        [], []);
}
