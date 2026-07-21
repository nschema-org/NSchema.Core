using NSchema.Plugins;
using NSchema.Plugins.Model;
using NSchema.Plugins.Model.LockFiles;

namespace NSchema.Tests.Plugins.Model;

/// <summary>
/// The <see cref="LockFile"/> domain type: its empty value and package lookup. File I/O lives in
/// <see cref="LockFileManager"/>.
/// </summary>
public sealed class LockFileTests
{
    [Fact]
    public void Empty_HasNoPlugins()
        => LockFile.Empty.Plugins.ShouldBeEmpty();

    [Fact]
    public void Find_ReturnsTheLockedEntry()
    {
        var lockFile = new LockFile([new LockedPlugin(new PackageId("NSchema.Postgres"), SemanticVersion.Parse("5.0.0-alpha.2"))]);

        lockFile.Find(new PackageId("NSchema.Postgres"))!.Version.ShouldBe(SemanticVersion.Parse("5.0.0-alpha.2"));
    }

    [Fact]
    public void Find_ReturnsNull_WhenUnlocked()
        => new LockFile([]).Find(new PackageId("NSchema.Aws")).ShouldBeNull();
}
