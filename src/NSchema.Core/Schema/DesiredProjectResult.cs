using NSchema.Diagnostics;
using NSchema.Schema.Model;

namespace NSchema.Schema;

/// <summary>
/// The outcome of reading the desired project.
/// </summary>
/// <param name="Project">The desired state declared by the DDL.</param>
/// <param name="Files">The full paths of the DDL files the project was read from, in load order.</param>
/// <param name="Diagnostics">Non-fatal findings raised while reading (for example, deprecated syntax).</param>
internal sealed record DesiredProjectResult(
    DesiredProject Project,
    IReadOnlyList<string> Files,
    IReadOnlyList<Diagnostic> Diagnostics
);
