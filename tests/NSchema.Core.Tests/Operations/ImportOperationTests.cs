using Microsoft.Extensions.Options;
using NSchema.Import;
using NSchema.Migration;
using NSchema.Operations;
using NSchema.Schema;
using NSchema.Schema.Model;

namespace NSchema.Tests.Operations;

public sealed class ImportOperationTests
{
    private readonly ICurrentSchemaProvider _currentSchema = Substitute.For<ICurrentSchemaProvider>();
    private readonly ISchemaImportTarget _target = Substitute.For<ISchemaImportTarget>();
    private readonly IMigrationReporter _reporter = Substitute.For<IMigrationReporter>();

    private readonly DatabaseSchema _schema = DatabaseSchema.Create([SchemaDefinition.Create("app",
        tables: [Table.Create("users"), Table.Create("orders")])]);

    private ImportOperation BuildSut(ImportOptions? opts = null) => new(
        Options.Create(opts ?? new ImportOptions()),
        _currentSchema,
        Helpers.TestImportTargets.ResolverFor(_target),
        Helpers.TestReporters.ResolverFor(_reporter));

    public ImportOperationTests()
    {
        _currentSchema
            .GetSchema(Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(_schema));
    }

    [Fact]
    public async Task Execute_FetchesSchemaFromOnlineSource()
    {
        await BuildSut().Execute(TestContext.Current.CancellationToken);

        await _currentSchema.Received(1).GetSchema(
            SchemaSourceMode.Online, Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_PassesSchemaFilterToSource()
    {
        var opts = new ImportOptions { Schemas = ["app", "audit"] };

        await BuildSut(opts).Execute(TestContext.Current.CancellationToken);

        await _currentSchema.Received(1).GetSchema(
            SchemaSourceMode.Online, opts.Schemas, Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WritesSchemaToTarget()
    {
        await BuildSut().Execute(TestContext.Current.CancellationToken);

        await _target.Received(1).Write(_schema, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WithTableFilter_FiltersSchemaBeforeWriting()
    {
        var opts = new ImportOptions { Tables = ["users"] };

        await BuildSut(opts).Execute(TestContext.Current.CancellationToken);

        await _target.Received(1).Write(
            Arg.Is<DatabaseSchema>(s =>
                s.Schemas.Single().Tables.Count == 1 &&
                s.Schemas.Single().Tables.Single().Name == "users"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WithEmptyTableFilter_WritesSchemaUnfiltered()
    {
        var opts = new ImportOptions { Tables = [] };

        await BuildSut(opts).Execute(TestContext.Current.CancellationToken);

        await _target.Received(1).Write(_schema, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ReportsProgress()
    {
        await BuildSut().Execute(TestContext.Current.CancellationToken);

        _reporter.Received(2).Info(Arg.Any<string>());
    }
}
