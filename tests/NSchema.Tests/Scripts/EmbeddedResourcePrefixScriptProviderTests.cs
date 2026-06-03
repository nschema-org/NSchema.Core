using System.Reflection;
using NSchema.Scripts;
using NSchema.Scripts.Model;

namespace NSchema.Tests.Scripts;

public sealed class EmbeddedResourcePrefixScriptProviderTests
{
    private static readonly Assembly _assembly = typeof(EmbeddedResourcePrefixScriptProviderTests).Assembly;

    [Fact]
    public async Task GetScripts_ReturnsAllMatchingResources_OrderedByName()
    {
        var sut = new EmbeddedResourcePrefixScriptProvider(
            ScriptType.PreDeployment, _assembly, "NSchema.Tests.Resources.pre_");

        var scripts = await sut.GetScripts(TestContext.Current.CancellationToken);

        scripts.Select(s => s.Name).ShouldBe(["pre_001", "pre_002"]);
        scripts.ShouldAllBe(s => s.Type == ScriptType.PreDeployment);
    }

    [Fact]
    public async Task GetScripts_IsCaseInsensitiveOnPrefix()
    {
        var sut = new EmbeddedResourcePrefixScriptProvider(
            ScriptType.PreDeployment, _assembly, "nschema.tests.resources.PRE_");

        var scripts = await sut.GetScripts(TestContext.Current.CancellationToken);

        scripts.Select(s => s.Name).ShouldBe(["pre_001", "pre_002"]);
    }

    [Fact]
    public async Task GetScripts_ReturnsEmpty_WhenNoResourcesMatchPrefix()
    {
        var sut = new EmbeddedResourcePrefixScriptProvider(
            ScriptType.PreDeployment, _assembly, "NSchema.Tests.Resources.nomatch_");

        var scripts = await sut.GetScripts(TestContext.Current.CancellationToken);

        scripts.ShouldBeEmpty();
    }
}
