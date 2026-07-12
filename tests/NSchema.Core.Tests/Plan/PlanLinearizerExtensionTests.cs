using NSchema.Diff.Domain.Models.Extensions;
using NSchema.Diff.Domain.Models.Schemas;
using NSchema.Diff.Domain.Models;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Models.Extensions;
using NSchema.Plan.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Extensions;

namespace NSchema.Tests.Plan;

/// <summary>
/// Pins the ordering of the root-level extension actions: extensions are created/updated before any schema or
/// object that may depend on them, and dropped only after everything else is gone.
/// </summary>
public sealed class PlanLinearizerExtensionTests
{
    private readonly PlanLinearizer _linearizer = new();

    private static DatabaseDiff Diff(IReadOnlyList<ExtensionDiff> extensions, params SchemaDiff[] schemas) =>
        new(schemas, extensions);

    [Fact]
    public void CreateExtension_IsEmittedBeforeSchemaCreation()
    {
        var actions = _linearizer.Linearize(Diff(
            [new ExtensionDiff("citext", ChangeKind.Add, Definition: new Extension("citext"))],
            new SchemaDiff("app", ChangeKind.Add)));

        var createExtension = actions.Select((a, i) => (a, i)).Single(x => x.a is CreateExtension).i;
        var createSchema = actions.Select((a, i) => (a, i)).Single(x => x.a is CreateSchema).i;
        createExtension.ShouldBeLessThan(createSchema);
    }

    [Fact]
    public void DropExtension_IsEmittedAfterSchemaDrop()
    {
        var actions = _linearizer.Linearize(Diff(
            [new ExtensionDiff("citext", ChangeKind.Remove)],
            new SchemaDiff("app", ChangeKind.Remove)));

        var dropExtension = actions.Select((a, i) => (a, i)).Single(x => x.a is DropExtension).i;
        var dropSchema = actions.Select((a, i) => (a, i)).Single(x => x.a is DropSchema).i;
        dropExtension.ShouldBeGreaterThan(dropSchema);
    }

    [Fact]
    public void AddedExtension_WithComment_EmitsCreateThenSetComment()
    {
        var actions = _linearizer.Linearize(Diff(
            [new ExtensionDiff("postgis", ChangeKind.Add, Definition: new Extension("postgis", Comment: "gis"),
                Comment: new ValueChange<string>(null, "gis"))]));

        actions.OfType<CreateExtension>().ShouldHaveSingleItem().Extension.Name.ShouldBe("postgis");
        actions.OfType<SetExtensionComment>().ShouldHaveSingleItem().NewComment.ShouldBe("gis");
    }

    [Fact]
    public void ModifiedExtension_VersionChange_EmitsAlterExtension()
    {
        var actions = _linearizer.Linearize(Diff(
            [new ExtensionDiff("postgis", ChangeKind.Modify, Version: new ValueChange<string>("3.3", "3.4"))]));

        var alter = actions.OfType<AlterExtension>().ShouldHaveSingleItem();
        alter.OldVersion.ShouldBe("3.3");
        alter.NewVersion.ShouldBe("3.4");
    }
}
