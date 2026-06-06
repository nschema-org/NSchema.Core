using NSchema.Resolution;
using NSchema.Sql;

namespace NSchema.Tests.Helpers;

internal static class TestSqlGenerators
{
    /// <summary>
    /// Wraps a single (optional) generator in an <see cref="IKeyedResolver{T}"/> so operation tests can drive
    /// the "generator present / absent" paths directly.
    /// </summary>
    public static IKeyedResolver<ISqlGenerator> ResolverFor(ISqlGenerator? generator)
    {
        var resolver = Substitute.For<IKeyedResolver<ISqlGenerator>>();
        resolver.HasCurrent.Returns(generator is not null);
        if (generator is not null)
        {
            resolver.Current.Returns(generator);
        }

        return resolver;
    }
}
