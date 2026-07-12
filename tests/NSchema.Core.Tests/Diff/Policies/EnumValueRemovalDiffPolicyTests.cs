using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Enums;
using NSchema.Diff.Domain.Models.Schemas;
using NSchema.Diff.Policies;

namespace NSchema.Tests.Diff.Policies;

public sealed class EnumValueRemovalDiffPolicyTests
{
    private readonly EnumValueRemovalDiffPolicy _sut = new();

    private static DatabaseDiff DiffWithEnum(EnumDiff enumDiff) =>
        new([new SchemaDiff("app", Enums: [enumDiff])]);

    private static EnumDiff ValueRemoval() => new("app", "status", ChangeKind.Modify,
        Values: new ValueChange<IReadOnlyList<string>>(["a", "b"], ["a"]));

    [Fact]
    public void Validate_ValueRemoval_IsAnError()
    {
        var diagnostic = _sut.Validate(DiffWithEnum(ValueRemoval())).ShouldHaveSingleItem();

        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.Source.ShouldBe("enum-value-removal");
        diagnostic.Message.ShouldContain("app.status");
        diagnostic.Message.ShouldContain("[a, b] -> [a]");
        diagnostic.Message.ShouldContain("Recreate the type manually");
    }

    [Fact]
    public void Validate_ValueAddition_PassesClean()
    {
        var addition = new EnumDiff("app", "status", ChangeKind.Modify,
            AddedValues: [new EnumValueAddition("b", After: "a")],
            Values: new ValueChange<IReadOnlyList<string>>(["a"], ["a", "b"]));

        _sut.Validate(DiffWithEnum(addition)).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WholeEnumRemoval_IsNotThisPolicysConcern()
        // A whole-enum drop is governed by the (configurable) destructive-action policy instead.
        => _sut.Validate(DiffWithEnum(new EnumDiff("app", "status", ChangeKind.Remove))).ShouldBeEmpty();

    [Fact]
    public void Validate_RenameAndCommentOnlyChange_PassesClean()
        => _sut.Validate(DiffWithEnum(new EnumDiff("app", "status", ChangeKind.Modify,
            RenamedFrom: "state", Comment: new ValueChange<string>("old", "new")))).ShouldBeEmpty();
}
