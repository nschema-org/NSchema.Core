using NSchema.Project.Nsql;
using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Diff.Domain;

namespace NSchema.Tests.EndToEnd;

/// <summary>
/// Round-trip drift gate: writing the desired schema out and reading it straight back must produce
/// <em>no</em> diff. This is the DB-free analogue of <c>import</c> → <c>plan</c> (and, once state captures
/// the applied schema, <c>apply</c> → <c>plan</c>): the trust contract that a clean cycle never shows a
/// phantom change. It runs the round-trip <em>through the comparer</em> — the component that actually
/// decides drift — rather than asserting structural equality, so it is the regression gate for the
/// serialization-fidelity bugs (e.g. <c>integer</c> vs <c>int</c>) that previously surfaced as drift.
/// </summary>
public sealed class RoundTripDriftTests
{
    private readonly SchemaComparer _comparer = new(NullLogger<SchemaComparer>.Instance);

    [Fact]
    public void DdlRoundTrip_OfRichSchema_ProducesNoDiff()
    {
        // Serialize every domain feature to DDL and read it straight back: the comparer must see no change.
        var original = TestData.RichSchema();
        var reparsed = new TestDdlParser(NsqlWriter.Write(original)).Parse().Schema;

        _comparer.Compare(original, reparsed).IsEmpty.ShouldBeTrue();
    }
}
