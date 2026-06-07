using NSchema.Operations;
using NSchema.Resolution;

namespace NSchema.Tests.Helpers;

internal static class TestReporters
{
    /// <summary>
    /// Wraps a single reporter in an <see cref="IKeyedResolver{T}"/> whose <c>Current</c> returns it.
    /// </summary>
    public static IKeyedResolver<IOperationReporter> ResolverFor(IOperationReporter reporter)
    {
        var resolver = Substitute.For<IKeyedResolver<IOperationReporter>>();
        resolver.Current.Returns(reporter);
        resolver.HasCurrent.Returns(true);
        return resolver;
    }
}
