using NSchema.Import;
using NSchema.Operations;
using NSchema.Operations.Import;
using NSchema.Resolution;
using NSchema.Schema;
using NSchema.Schema.Model;

namespace NSchema.Tests.Operations.Import;

public sealed class ImportOperationTests
{
    private readonly ICurrentSchemaProvider _currentSchema = Substitute.For<ICurrentSchemaProvider>();
    private readonly ISchemaImportTarget _target = Substitute.For<ISchemaImportTarget>();
    private readonly IKeyedResolver<ISchemaImportTarget> _targets;
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();

    private readonly DatabaseSchema _schema = DatabaseSchema.Create([SchemaDefinition.Create("app",
        tables: [Table.Create("users"), Table.Create("orders")])]);

    private ImportOperation BuildSut() => new(
        _currentSchema,
        _targets,
        Helpers.TestReporters.ResolverFor(_reporter));

    public ImportOperationTests()
    {
        _targets = Helpers.TestImportTargets.ResolverFor(_target);
        _currentSchema
            .GetSchema(Arg.Any<SchemaSourceMode>(), Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(_schema));
    }

    [Fact]
    public async Task Execute_FetchesSchemaFromOnlineSource()
    {
        await BuildSut().Execute(new ImportArguments(), TestContext.Current.CancellationToken);

        await _currentSchema.Received(1).GetSchema(
            SchemaSourceMode.Online, Arg.Any<string[]?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_PassesSchemaFilterToSource()
    {
        var arguments = new ImportArguments { Schemas = ["app", "audit"] };

        await BuildSut().Execute(arguments, TestContext.Current.CancellationToken);

        await _currentSchema.Received(1).GetSchema(
            SchemaSourceMode.Online, arguments.Schemas, Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WritesSchemaToTarget()
    {
        await BuildSut().Execute(new ImportArguments(), TestContext.Current.CancellationToken);

        await _target.Received(1).Write(_schema, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ResolvesTargetFromArguments()
    {
        await BuildSut().Execute(new ImportArguments { Target = "warehouse" }, TestContext.Current.CancellationToken);

        _targets.Received(1).Resolve("warehouse");
    }

    [Fact]
    public async Task Execute_WithTableFilter_FiltersSchemaBeforeWriting()
    {
        var arguments = new ImportArguments { Tables = ["users"] };

        await BuildSut().Execute(arguments, TestContext.Current.CancellationToken);

        await _target.Received(1).Write(
            Arg.Is<DatabaseSchema>(s =>
                s.Schemas.Single().Tables.Count == 1 &&
                s.Schemas.Single().Tables.Single().Name == "users"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WithEmptyTableFilter_WritesSchemaUnfiltered()
    {
        var arguments = new ImportArguments { Tables = [] };

        await BuildSut().Execute(arguments, TestContext.Current.CancellationToken);

        await _target.Received(1).Write(_schema, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ReportsProgress()
    {
        await BuildSut().Execute(new ImportArguments(), TestContext.Current.CancellationToken);

        _reporter.Received(2).Info(Arg.Any<string>());
    }
}
