using NSchema.Configuration;
using NSchema.Schema.Model;

namespace NSchema.Schema.Serialization.Ddl;

/// <summary>
/// The full result of parsing a DSL source: the desired-state <see cref="Schema"/> plus any top-level config.
/// </summary>
internal sealed record DslDocument(DatabaseSchema Schema, IReadOnlyList<ConfigBlock> Config);
