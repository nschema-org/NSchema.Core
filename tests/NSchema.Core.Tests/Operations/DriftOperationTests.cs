using NSchema.Deployment;
using NSchema.Diff.Model;
using NSchema.Diff.Model.Schemas;
using NSchema.Diff.Model.Services;
using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Operations;
using NSchema.Operations.Progress;
using NSchema.Project.Model.Directives;
using NSchema.State;
using NSchema.State.Backends;
using NSchema.State.Model;

namespace NSchema.Tests.Operations;

public sealed class DriftOperationTests
{
    private readonly IDatabaseProvider _provider = Substitute.For<IDatabaseProvider>();
    private readonly IDatabaseStateStore _store = Substitute.For<IDatabaseStateStore>();
    private readonly IDatabaseStateSerializer _serializer = new DatabaseStateSerializer();
    private readonly IDatabaseComparer _comparer = Substitute.For<IDatabaseComparer>();
    private readonly IProgress<OperationProgress> _progress = Substitute.For<IProgress<OperationProgress>>();

    private readonly Database _recorded = new([new Schema(new SqlIdentifier("app"))]);
    private readonly Database _live = new([new Schema(new SqlIdentifier("app")), new Schema(new SqlIdentifier("audit"))]);
    private readonly DatabaseDiff _diff = new([new SchemaDiff(new SqlIdentifier("audit"), ChangeKind.Add)]);

    private readonly DriftOperation _sut;

    public DriftOperationTests()
    {
        // Recorded state comes from the store; live comes from the provider.
        _store.Read(Arg.Any<CancellationToken>()).Returns(_serializer.Serialize(new DatabaseState(_recorded)));
        _provider.GetDatabase(Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(_live));
        _comparer.Compare(Arg.Any<Database>(), Arg.Any<Database>(), Arg.Any<ProjectDirectives>()).Returns(_diff);

        _sut = new DriftOperation(_provider, new DatabaseStateManager(_serializer, _store), _comparer, _progress);
    }

    private static DriftArguments Args(PlanningScope? scope = null) => new() { Scope = scope ?? PlanningScope.All };

    [Fact]
    public async Task Execute_DiffsRecordedAgainstLive()
    {
        // Act
        await _sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert — direction is recorded -> live, so the diff describes how live drifted from the recorded state.
        // The recorded database round-trips through the state store, so match it by content, not reference.
        _comparer.Received(1).Compare(
            Arg.Is<Database>(d => d!.Schemas.Select(s => s.Name.Value).SequenceEqual(new[] { "app" })), _live, ProjectDirectives.Empty);
    }

    [Fact]
    public async Task Execute_ReadsRecordedFromState_AndLiveFromProvider()
    {
        // Act
        await _sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert
        await _store.Received(1).Read(Arg.Any<CancellationToken>());
        await _provider.Received(1).GetDatabase(Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ForwardsScopeToLive_AndFiltersRecorded()
    {
        // Arrange — recorded state holds an out-of-scope schema; the scoped read must drop it before diffing.
        _store.Read(Arg.Any<CancellationToken>())
            .Returns(_serializer.Serialize(new DatabaseState(new Database([new Schema(new SqlIdentifier("app")), new Schema(new SqlIdentifier("other"))]))));

        // Act
        await _sut.Execute(Args(PlanningScope.To(new SqlIdentifier("app"))), TestContext.Current.CancellationToken);

        // Assert — live gets the scope directly; recorded is filtered to it before the comparer sees it.
        await _provider.Received(1).GetDatabase(Arg.Is<PlanningScope>(s => s!.Contains(new SqlIdentifier("app")) && !s.IsUnscoped), Arg.Any<CancellationToken>());
        _comparer.Received(1).Compare(Arg.Is<Database>(d => d!.Schemas.Select(s => s.Name.Value).SequenceEqual(new[] { "app" })), _live, ProjectDirectives.Empty);
    }

    [Fact]
    public async Task Execute_NoRecordedState_DiffsAgainstAnEmptyDatabase()
    {
        // Arrange — before anything is recorded, drift measures the whole live database as new.
        _store.Read(Arg.Any<CancellationToken>()).Returns((ReadOnlyMemory<byte>?)null);

        // Act
        await _sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert — the recorded side is an empty database.
        _comparer.Received(1).Compare(Arg.Is<Database>(d => d!.Schemas.Count == 0), _live, ProjectDirectives.Empty);
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
        _comparer.Compare(Arg.Any<Database>(), Arg.Any<Database>(), Arg.Any<ProjectDirectives>()).Returns(new DatabaseDiff([]));

        // Act
        var result = await _sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.HasDrift.ShouldBeFalse();
    }
}
