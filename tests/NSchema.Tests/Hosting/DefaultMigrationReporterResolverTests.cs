using Microsoft.Extensions.Options;
using NSchema.Diff.Model;
using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Policies;
using NSchema.Sql.Model;

namespace NSchema.Tests.Hosting;

public sealed class DefaultMigrationReporterResolverTests
{
    /// <summary>A no-op reporter that only carries a format, for resolution tests.</summary>
    private sealed class StubReporter(string format) : IMigrationReporter
    {
        public string Format => format;
        public void Info(string message) { }
        public void Error(string message) { }
        public void ReportDiff(MigrationDiff diff) { }
        public void ReportSqlPlan(SqlPlan plan) { }
        public void ReportDiagnostics(PolicyDiagnostics diagnostics) { }
    }

    private static DefaultMigrationReporterResolver Resolver(
        string outputFormat, params IMigrationReporter[] reporters)
        => new(Options.Create(new MigrationRunOptions { OutputFormat = outputFormat }), reporters);

    [Fact]
    public void ForFormat_ReturnsRegisteredReporter()
    {
        var human = new StubReporter("human");
        var sut = Resolver("human", human, new StubReporter("json"));

        sut.ForFormat("human").ShouldBeSameAs(human);
    }

    [Fact]
    public void ForFormat_IsCaseInsensitive()
    {
        var json = new StubReporter("json");
        var sut = Resolver("human", json);

        sut.ForFormat("JSON").ShouldBeSameAs(json);
    }

    [Fact]
    public void ForFormat_LastRegistrationWins_ForDuplicateFormat()
    {
        var first = new StubReporter("human");
        var second = new StubReporter("human");
        var sut = Resolver("human", first, second);

        sut.ForFormat("human").ShouldBeSameAs(second);
    }

    [Fact]
    public void ForFormat_UnknownFormat_ThrowsWithAvailableFormats()
    {
        var sut = Resolver("human", new StubReporter("human"), new StubReporter("json"));

        var ex = Should.Throw<InvalidOperationException>(() => sut.ForFormat("xml"));

        ex.Message.ShouldContain("xml");
        ex.Message.ShouldContain("human");
        ex.Message.ShouldContain("json");
    }

    [Fact]
    public void Current_SelectsReporterForConfiguredOutputFormat()
    {
        var human = new StubReporter("human");
        var json = new StubReporter("json");
        var sut = Resolver("json", human, json);

        sut.Current.ShouldBeSameAs(json);
    }

    [Fact]
    public void Current_UnknownConfiguredFormat_Throws()
    {
        var sut = Resolver("json", new StubReporter("human"));

        Should.Throw<InvalidOperationException>(() => sut.Current);
    }

    [Fact]
    public void TryForFormat_ReturnsFalse_WhenUnregistered()
    {
        var sut = Resolver("human", new StubReporter("human"));

        sut.TryForFormat("json", out var reporter).ShouldBeFalse();
        reporter.ShouldBeNull();
    }

    [Fact]
    public void AvailableFormats_ListsDistinctFormats()
    {
        var sut = Resolver("human", new StubReporter("human"), new StubReporter("json"), new StubReporter("HUMAN"));

        sut.AvailableFormats.ShouldBe(["human", "json"], ignoreOrder: true);
    }
}
