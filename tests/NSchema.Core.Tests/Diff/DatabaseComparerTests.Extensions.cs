using NSchema.Project.Domain.Models;
using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Extensions;
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
        IReadOnlyList<Extension> desired,
        IReadOnlyList<SqlIdentifier>? dropped = null) =>
        Compare(
            new Database(Extensions: current),
            new Database(Extensions: desired),
            dropped is null ? null : new ProjectDirectives(Extensions: new NSchema.Project.Domain.Models.Extensions.ExtensionDirectives(Drops: dropped)))
        .Extensions.SingleOrDefault();

    [Fact]
    public void Compare_NewExtension_IsAddCarryingDefinition()
    {
        var diff = DiffExtensions([], [new Extension(new SqlIdentifier("postgis"), Version: "3.4")]);

        diff!.Kind.ShouldBe(ChangeKind.Add);
        diff.Definition!.Name.ShouldBe("postgis");
        diff.Definition.Version.ShouldBe("3.4");
    }

    [Fact]
    public void Compare_ExtensionPresentButNotDesired_IsLeftAlone()
        // Extensions are shared infrastructure (e.g. plpgsql is installed in every database); absence from the
        // desired set must never imply a drop. Only an explicit DROP removes one.
        => DiffExtensions([new Extension(new SqlIdentifier("plpgsql"))], []).ShouldBeNull();

    [Fact]
    public void Compare_ExplicitlyDroppedExtension_IsRemove()
    {
        var diff = DiffExtensions([new Extension(new SqlIdentifier("citext"))], [], dropped: [new SqlIdentifier("citext")]);

        diff!.Kind.ShouldBe(ChangeKind.Remove);
        diff.Name.ShouldBe("citext");
    }

    [Fact]
    public void Compare_DroppedExtensionNotInstalled_ProducesNoDiff()
        // Dropping something that isn't there is a no-op, not a phantom removal.
        => DiffExtensions([], [], dropped: [new SqlIdentifier("citext")]).ShouldBeNull();

    [Fact]
    public void Compare_UnchangedExtension_ProducesNoDiff()
        => DiffExtensions([new Extension(new SqlIdentifier("citext"))], [new Extension(new SqlIdentifier("citext"))]).ShouldBeNull();

    [Fact]
    public void Compare_OmittedDesiredVersion_IsNotComparedAgainstInstalledVersion()
        // A null desired version means "accept whatever is installed", so an omitted version can never drift.
        => DiffExtensions([new Extension(new SqlIdentifier("postgis"), Version: "3.4")], [new Extension(new SqlIdentifier("postgis"))]).ShouldBeNull();

    [Fact]
    public void Compare_ExtensionVersionChange_CarriesOldAndNewVersion()
    {
        var diff = DiffExtensions([new Extension(new SqlIdentifier("postgis"), Version: "3.3")], [new Extension(new SqlIdentifier("postgis"), Version: "3.4")]);

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.Version.ShouldBe(new ValueChange<string>("3.3", "3.4"));
    }

    [Fact]
    public void Compare_ExtensionCommentOnlyChange_IsModify()
    {
        var diff = DiffExtensions([new Extension(new SqlIdentifier("citext"), Comment: "old")], [new Extension(new SqlIdentifier("citext"), Comment: "new")]);

        diff!.Kind.ShouldBe(ChangeKind.Modify);
        diff.Comment.ShouldBe(new ValueChange<string>("old", "new"));
        diff.Version.ShouldBeNull();
    }
}
