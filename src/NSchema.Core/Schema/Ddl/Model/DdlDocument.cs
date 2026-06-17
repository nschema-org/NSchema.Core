using NSchema.Configuration;
using NSchema.Schema.Model;

namespace NSchema.Schema.Ddl.Model;

/// <summary>
/// The full result of parsing a DDL source file.
/// </summary>
public sealed record DdlDocument(DatabaseSchema Schema, IReadOnlyList<ConfigBlock> Config);
