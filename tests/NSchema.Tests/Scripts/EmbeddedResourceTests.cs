using System.Reflection;
using NSchema.Scripts;

namespace NSchema.Tests.Scripts;

public sealed class EmbeddedResourceTests
{
    private static readonly Assembly _assembly = typeof(EmbeddedResourceTests).Assembly;

    // -------------------------------------------------------------------------
    // Read
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Read_ReturnsResourceContent()
    {
        var content = await EmbeddedResource.Read(
            _assembly, "NSchema.Tests.Resources.pre_001.sql", TestContext.Current.CancellationToken);

        content.ShouldBe("CREATE EXTENSION IF NOT EXISTS pgcrypto;\n");
    }

    [Fact]
    public async Task Read_WhenResourceMissing_ThrowsInvalidOperationWithResourceAndAssemblyNames()
    {
        var act = () => EmbeddedResource.Read(_assembly, "NSchema.Tests.Resources.nope.sql", TestContext.Current.CancellationToken);

        var ex = await act.ShouldThrowAsync<InvalidOperationException>();
        ex.Message.ShouldContain("NSchema.Tests.Resources.nope.sql");
        ex.Message.ShouldContain("NSchema.Tests");
    }

    // -------------------------------------------------------------------------
    // DeriveName
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("NSchema.Tests.Resources.pre_001.sql", "pre_001")]
    [InlineData("Some.Deeply.Nested.Namespace.script.sql", "script")]
    [InlineData("script.sql", "script")]
    public void DeriveName_ExtractsSegmentBeforeExtension(string resourceName, string expected)
        => EmbeddedResource.DeriveName(resourceName).ShouldBe(expected);
}
