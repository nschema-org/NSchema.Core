using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Triggers;

namespace NSchema.Tests.Helpers;

/// <summary>
/// Shouldly bridges for <see cref="SqlIdentifier"/>: asserting an identifier against a string literal compares
/// the exact written text, so tests keep witnessing that casing is preserved end-to-end.
/// </summary>
internal static class ShouldlyIdentifierExtensions
{
    public static void ShouldBe(this SqlIdentifier actual, string? expected) => actual.Value.ShouldBe(expected);

    public static void ShouldBe(this SqlIdentifier? actual, string? expected) => (actual?.Value).ShouldBe(expected);

    public static void ShouldBe(this IEnumerable<SqlIdentifier>? actual, IEnumerable<string>? expected, bool ignoreOrder = false) =>
        actual?.Select(i => i.Value).ShouldBe(expected, ignoreOrder);

    public static void ShouldBe(this RoutineReference? actual, string? expected) => (actual?.ToString()).ShouldBe(expected);
}
