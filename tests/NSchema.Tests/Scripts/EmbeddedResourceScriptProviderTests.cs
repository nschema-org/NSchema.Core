using System.Reflection;
using NSchema.Scripts;
using NSchema.Scripts.Model;

namespace NSchema.Tests.Scripts;

public sealed class EmbeddedResourceScriptProviderTests
{
    private static readonly Assembly _assembly = typeof(EmbeddedResourceScriptProviderTests).Assembly;

    [Fact]
    public async Task GetScripts_LoadsResourceWithDerivedName()
    {
        var sut = new EmbeddedResourceScriptProvider(
            ScriptType.PreDeployment, _assembly, "NSchema.Tests.Resources.pre_001.sql");

        var script = (await sut.GetScripts(TestContext.Current.CancellationToken)).ShouldHaveSingleItem();

        script.Name.ShouldBe("pre_001");
        script.Sql.ShouldBe("CREATE EXTENSION IF NOT EXISTS pgcrypto;\n");
        script.Type.ShouldBe(ScriptType.PreDeployment);
    }

    [Fact]
    public async Task GetScripts_UsesExplicitName_WhenProvided()
    {
        var sut = new EmbeddedResourceScriptProvider(
            ScriptType.PostDeployment, _assembly, "NSchema.Tests.Resources.post_001.sql", name: "analyze");

        var script = (await sut.GetScripts(TestContext.Current.CancellationToken)).ShouldHaveSingleItem();

        script.Name.ShouldBe("analyze");
    }

    [Fact]
    public async Task GetScripts_WhenResourceMissing_Throws()
    {
        var sut = new EmbeddedResourceScriptProvider(
            ScriptType.PreDeployment, _assembly, "NSchema.Tests.Resources.missing.sql");

        var act = () => sut.GetScripts().AsTask();

        await act.ShouldThrowAsync<InvalidOperationException>();
    }
}
