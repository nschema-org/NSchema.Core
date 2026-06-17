using NSchema.Configuration;
using NSchema.Schema.Model;

namespace NSchema.Schema.Serialization.Ddl;

/// <summary>
/// The full result of parsing a DDL source file.
/// </summary>
internal sealed record DdlDocument(DatabaseSchema Schema, IReadOnlyList<ConfigBlock> Config);
