using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Triggers;

namespace NSchema.Tests.Helpers;

/// <summary>
/// Shouldly bridges for <see cref="ValueObject"/>s: asserting a value object against a string literal compares
/// the exact underlying text, so tests keep witnessing that casing is preserved end-to-end.
/// </summary>
internal static class ShouldlyIdentifierExtensions
{
    extension<T>(ValueObject<T>? actual)
    {
        // A null receiver still asserts (against default), so a missing value fails rather than silently passing.
        public void ShouldBe(T? expected) => (actual is null ? default : actual.Value).ShouldBe(expected);
    }

    extension<T>(IEnumerable<ValueObject<T>>? actual)
    {
        public void ShouldBe(IEnumerable<T>? expected, bool ignoreOrder = false)
        {
            var actualValue = actual?.Select(i => i.Value) ?? [];
            actualValue.ShouldBe(expected, ignoreOrder);
        }
    }

    extension(RoutineReference? actual)
    {
        public void ShouldBe(string? expected) => (actual?.ToString()).ShouldBe(expected);
    }
}
