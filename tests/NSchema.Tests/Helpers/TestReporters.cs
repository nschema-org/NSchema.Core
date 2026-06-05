using NSchema.Migration;
using NSchema.Resolution;

namespace NSchema.Tests.Helpers;

internal static class TestReporters
{
    /// <summary>
    /// Wraps a single reporter in an <see cref="IKeyedResolver{T}"/> whose <c>Current</c> returns it.
    /// </summary>
    public static IKeyedResolver<IMigrationReporter> ResolverFor(IMigrationReporter reporter)
    {
        var resolver = Substitute.For<IKeyedResolver<IMigrationReporter>>();
        resolver.Current.Returns(reporter);
        resolver.HasCurrent.Returns(true);
        return resolver;
    }
}
