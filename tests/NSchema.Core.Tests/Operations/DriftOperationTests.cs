using NSchema.Current;
using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Schemas;
using NSchema.Operations;
using NSchema.Operations.Progress;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Schemas;

namespace NSchema.Tests.Operations;

public sealed class DriftOperationTests
{
    private readonly ICurrentSchemaProvider _currentProvider = Substitute.For<ICurrentSchemaProvider>();
    private readonly ISchemaComparer _comparer = Substitute.For<ISchemaComparer>();
    private readonly IProgress<OperationProgress> _progress = Substitute.For<IProgress<OperationProgress>>();

    private readonly DatabaseSchema _recorded = new([new SchemaDefinition("app")]);
    private readonly DatabaseSchema _live = new([new SchemaDefinition("app"), new SchemaDefinition("audit")]);
    private readonly DatabaseDiff _diff = new([new SchemaDiff("audit", ChangeKind.Add)]);

    private readonly DriftOperation _sut;

    public DriftOperationTests()
    {
        _currentProvider.GetSchema(SchemaSourceMode.Offline, Arg.Any<SchemaScope>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(_recorded));
        _currentProvider.GetSchema(SchemaSourceMode.Online, Arg.Any<SchemaScope>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(_live));
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>()).Returns(_diff);

        _sut = new DriftOperation(_currentProvider, _comparer, _progress);
    }

    private static DriftArguments Args(SchemaScope? scope = null) => new() { Scope = scope ?? SchemaScope.All };

    [Fact]
    public async Task Execute_DiffsRecordedAgainstLive()
    {
        // Act
        await _sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert — direction is recorded -> live, so the diff describes how live drifted from the recorded state.
        _comparer.Received(1).Compare(_recorded, _live);
    }

    [Fact]
    public async Task Execute_ReadsRecordedOffline_AndLiveOnline_BothRequired()
    {
        // Act
        await _sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert
        await _currentProvider.Received(1).GetSchema(SchemaSourceMode.Offline, Arg.Any<SchemaScope>(), Arg.Any<CancellationToken>());
        await _currentProvider.Received(1).GetSchema(SchemaSourceMode.Online, Arg.Any<SchemaScope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ForwardsScopeToBothReads()
    {
        // Act
        await _sut.Execute(Args(SchemaScope.Of("app")), TestContext.Current.CancellationToken);

        // Assert
        await _currentProvider.Received(1).GetSchema(SchemaSourceMode.Offline, Arg.Is<SchemaScope>(s => s.Includes("app") && !s.IsAll), Arg.Any<CancellationToken>());
        await _currentProvider.Received(1).GetSchema(SchemaSourceMode.Online, Arg.Is<SchemaScope>(s => s.Includes("app") && !s.IsAll), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ReturnsTheComparerDiff_AndReportsDrift()
    {
        // Act
        var result = await _sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Diff.ShouldBe(_diff);
        result.Value!.HasDrift.ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_WhenDiffEmpty_ReportsNoDrift()
    {
        // Arrange
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>()).Returns(new DatabaseDiff([]));

        // Act
        var result = await _sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.HasDrift.ShouldBeFalse();
    }
}
