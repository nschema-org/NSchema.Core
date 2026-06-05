using NSchema.Sql;

namespace NSchema.Tests.Helpers;

internal static class TestSqlGenerators
{
    /// <summary>
    /// Wraps a single (optional) generator in an <see cref="ISqlGeneratorResolver"/> whose <c>Current</c>
    /// returns it, so operation tests can drive the "generator present / absent" paths directly.
    /// </summary>
    public static ISqlGeneratorResolver ResolverFor(ISqlGenerator? generator)
    {
        var resolver = Substitute.For<ISqlGeneratorResolver>();
        resolver.Current.Returns(generator);
        return resolver;
    }
}
