using NSchema.Import;
using NSchema.Resolution;

namespace NSchema.Tests.Helpers;

internal static class TestImportTargets
{
    public static IKeyedResolver<ISchemaImportTarget> ResolverFor(ISchemaImportTarget target)
    {
        var resolver = Substitute.For<IKeyedResolver<ISchemaImportTarget>>();
        resolver.Current.Returns(target);
        resolver.HasCurrent.Returns(true);
        return resolver;
    }
}
