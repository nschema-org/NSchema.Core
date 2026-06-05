using NSchema.Hosting;
using NSchema.Migration;

namespace NSchema.Tests.Helpers;

internal static class TestReporters
{
    /// <summary>
    /// Wraps a single reporter in an <see cref="IMigrationReporterResolver"/> whose <c>Current</c> (and any
    /// format lookup) returns it, so consumer tests can keep asserting on the reporter directly.
    /// </summary>
    public static IMigrationReporterResolver ResolverFor(IMigrationReporter reporter)
    {
        var resolver = Substitute.For<IMigrationReporterResolver>();
        resolver.Current.Returns(reporter);
        resolver.ForFormat(Arg.Any<string>()).Returns(reporter);
        return resolver;
    }
}
