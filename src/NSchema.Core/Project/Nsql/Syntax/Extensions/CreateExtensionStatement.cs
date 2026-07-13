namespace NSchema.Project.Nsql.Syntax.Extensions;

/// <summary>
/// <c>CREATE EXTENSION name [VERSION 'version'];</c> — the name may be written bare or quoted
/// (<c>'uuid-ossp'</c>) since extension names commonly contain characters a bare identifier cannot.
/// </summary>
/// <param name="Name">The extension name.</param>
/// <param name="Version">The <c>VERSION</c> string, or <see langword="null"/>.</param>
public sealed record CreateExtensionStatement(Identifier Name, string? Version = null) : NsqlStatement;
