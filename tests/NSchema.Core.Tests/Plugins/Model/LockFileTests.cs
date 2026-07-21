using NSchema.Plugins.Model;

namespace NSchema.Tests.Plugins.Model;

/// <summary>
/// The plugin lockfile: Core owns the format both ways, so <see cref="LockFile.Write"/> output round-trips
/// through <see cref="LockFile.Read"/>, and a newer lockfile's unknown attributes are ignored rather than rejected.
/// </summary>
public sealed class LockFileTests
{
    private static readonly LockedPlugin[] Pins =
    [
        new(new PackageId("NSchema.Postgres"), "5.0.0-alpha.2"),
        new(new PackageId("NSchema.Aws"), "5.0.0-alpha.2"),
    ];

    [Fact]
    public void Write_ThenRead_RoundTrips()
    {
        var text = LockFile.Write(Pins);

        var result = LockFile.Read(text);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(Pins);
    }

    [Fact]
    public void Write_IsOneStatementPerPin_WithAManagedHeader()
    {
        var text = LockFile.Write(Pins);

        text.ShouldContain("-- nschema.lock");
        text.ShouldContain("LOCK ( source = 'NSchema.Postgres', version = '5.0.0-alpha.2' );");
        text.ShouldContain("LOCK ( source = 'NSchema.Aws', version = '5.0.0-alpha.2' );");
    }

    [Fact]
    public void Read_Empty_IsAnEmptyList()
        => LockFile.Read("").Value.ShouldBeEmpty();

    [Fact]
    public void Read_IgnoresUnknownAttributes()
    {
        // A newer NSchema may add attributes (e.g. a content hash); an older reader keeps loading.
        var result = LockFile.Read("LOCK ( source = 'NSchema.Postgres', version = '5.0.0', hash = 'sha512-abc' );");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldHaveSingleItem().ShouldBe(new LockedPlugin(new PackageId("NSchema.Postgres"), "5.0.0"));
    }

    [Fact]
    public void Read_MissingSource_IsAnError()
        => LockFile.Read("LOCK ( version = '5.0.0' );")
            .Errors.ShouldHaveSingleItem().Message.ShouldContain("requires a 'source' attribute");

    [Fact]
    public void Read_MissingVersion_IsAnError()
        => LockFile.Read("LOCK ( source = 'NSchema.Postgres' );")
            .Errors.ShouldHaveSingleItem().Message.ShouldContain("requires a 'version' attribute");

    [Fact]
    public void Read_InvalidPackageId_IsAnError()
        => LockFile.Read("LOCK ( source = 'not a package', version = '5.0.0' );")
            .Errors.ShouldHaveSingleItem().Message.ShouldContain("not a valid package id");
}
