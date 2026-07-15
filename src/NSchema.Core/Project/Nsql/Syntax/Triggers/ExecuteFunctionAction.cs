using NSchema.Model;

namespace NSchema.Project.Nsql.Syntax.Triggers;

/// <summary>
/// <c>EXECUTE FUNCTION|PROCEDURE [schema.]function(arguments)</c> — the function reference stays as
/// written (unqualified resolves via the engine's search path).
/// </summary>
/// <param name="Function">The function reference as written.</param>
/// <param name="Arguments">The argument list, verbatim (usually empty).</param>
public sealed record ExecuteFunctionAction(QualifiedName Function, SqlText Arguments) : TriggerAction;
