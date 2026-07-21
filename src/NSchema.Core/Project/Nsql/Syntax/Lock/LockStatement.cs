using NSchema.Project.Nsql.Syntax.Config;

namespace NSchema.Project.Nsql.Syntax.Lock;

/// <summary>
/// <c>LOCK ( source = '…', version = '…' );</c> — one recorded plugin pin: the package the project resolved
/// (<c>source</c>) and the exact version it locked to. The lockfile is machine-managed; a <c>LOCK</c> is not a
/// configuration statement and never appears in a configuration file.
/// </summary>
/// <param name="Attributes">The attribute list.</param>
public sealed record LockStatement(IReadOnlyList<ConfigAttribute> Attributes) : NsqlStatement;
