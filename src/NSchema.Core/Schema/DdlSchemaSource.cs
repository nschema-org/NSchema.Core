using Microsoft.Extensions.FileSystemGlobbing;

namespace NSchema.Schema;

/// <summary>
/// A registered source of desired-state DDL: a base directory and the glob matcher selecting its <c>.sql</c> files.
/// </summary>
internal sealed record DdlSchemaSource(string BaseDirectory, Matcher Matcher);
