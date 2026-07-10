using NSchema.Schema.Model;
using NSchema.Sql.Model;
using NSchema.State.Model;

namespace NSchema.Tests.State;

public sealed class SchemaStateTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RecordExecutions_AddsEntriesToTheLedger()
    {
        // Arrange
        var state = SchemaState.Empty;

        // Act
        var recorded = state.RecordExecutions([new ScriptHash("seed", "abc")], _now);

        // Assert
        recorded.ExecutedScripts.ShouldHaveSingleItem().ShouldBe(new ScriptExecutionRecord("seed", "abc", _now));
    }

    [Fact]
    public void RecordExecutions_ReplacesAnEarlierExecutionByName_CaseInsensitively()
    {
        // Arrange
        var state = new SchemaState(new DatabaseSchema(), [new ScriptExecutionRecord("Seed", "old", DateTimeOffset.UnixEpoch)]);

        // Act
        var recorded = state.RecordExecutions([new ScriptHash("seed", "new")], _now);

        // Assert
        recorded.ExecutedScripts.ShouldHaveSingleItem().Hash.ShouldBe("new");
    }

    [Fact]
    public void RecordExecutions_LeavesOtherEntriesAlone()
    {
        // Arrange
        var existing = new ScriptExecutionRecord("api-login", "hash", DateTimeOffset.UnixEpoch);
        var state = new SchemaState(new DatabaseSchema(), [existing]);

        // Act
        var recorded = state.RecordExecutions([new ScriptHash("seed", "abc")], _now);

        // Assert
        recorded.ExecutedScripts.ShouldBe([existing, new ScriptExecutionRecord("seed", "abc", _now)]);
    }

    [Fact]
    public void RecordExecutions_NothingExecuted_ReturnsTheSameState()
        => SchemaState.Empty.RecordExecutions([], _now).ShouldBeSameAs(SchemaState.Empty);
}
