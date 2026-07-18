using NSchema.Diff.Model;
using NSchema.Diff.Model.Extensions;
using NSchema.Model;
using NSchema.Model.Extensions;

namespace NSchema.Tests.Diff;

public partial class DatabaseComparerTests
{
    // -------------------------------------------------------------------------
    // Extensions (database-global, root-level)
    // -------------------------------------------------------------------------

    /// <summary>Diffs two databases holding the given root-level extensions, returning the single extension diff.</summary>
    private ExtensionDiff? DiffExtensions(
        IReadOnlyList<Extension> current,
        IReadOnlyList<Extension> desired) =>
        Compare(new Database { Extensions = [.. current] }, new Database { Extensions = [.. desired] })
        .Extensions.SingleOrDefault();

    [Fact]
    public void Compare_NewExtension_IsAddCarryingDefinition()
    {
        var diff = DiffExtensions([], [new Extension { Name = new SqlIdentifier("postgis"), Version = "3.4" }]);

        diff!.Kind.ShouldBe(ChangeKind.Add);
        diff.Definition!.Name.ShouldBe("postgis");
        diff.Definition.Version.ShouldBe("3.4");
    }

    [Fact]
    public void Compare_ExtensionPresentButNotDesired_IsRemove()
    {
        // The current side only ever contains managed extensions, so absence from the desired set is a
        // removal like any other object; unmanaged shared infrastructure never enters the compare.
        var diff = DiffExtensions([new Extension { Name = new SqlIdentifier("citext") }], []);

        diff!.Kind.ShouldBe(ChangeKind.Remove);
        diff.Name.ShouldBe("citext");
    }

    [Fact]
    public void Compare_UnchangedExtension_ProducesNoDiff()
        => DiffExtensions([new Extension { Name = new SqlIdentifier("citext") }], [new Extension { Name = new SqlIdentifier("citext") }]).ShouldBeNull();

    [Fact]
    public void Compare_OmittedDesiredVersion_IsNotComparedAgainstInstalledVersion()
        // A null desired version means "accept whatever is installed", so an omitted version can never drift.
        => DiffExtensions([new Extension { Name = new SqlIdentifier("postgis"), Version = "3.4" }], [new Extension { Name = new SqlIdentifier("postgis") }]).ShouldBeNull();

    [Fact]
    public void Compare_ExtensionVersionChange_CarriesOldAndNewVersion()
    {
        var diff = DiffExtensions([new Extension { Name = new SqlIdentifier("postgis"), Version = "3.3" }], [new Extension { Name = new SqlIdentifier("postgis"), Version = "3.4" }]);

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.Version.ShouldBe(new ValueChange<string>("3.3", "3.4"));
    }

    [Fact]
    public void Compare_ExtensionCommentOnlyChange_IsModify()
    {
        var diff = DiffExtensions([new Extension { Name = new SqlIdentifier("citext"), Comment = "old" }], [new Extension { Name = new SqlIdentifier("citext"), Comment = "new" }]);

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.Comment.ShouldBe(new ValueChange<string>("old", "new"));
        diff.Version.ShouldBeNull();
    }
}
