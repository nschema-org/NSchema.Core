namespace NSchema.Project.Nsql.Syntax;

/// <summary>
/// An optionally schema-qualified name as written. Inside a template body an unqualified name binds to
/// the applied schema at projection; at the top level qualification is required by the grammar.
/// </summary>
/// <param name="Schema">The schema qualifier, or <see langword="null"/> when written unqualified.</param>
/// <param name="Name">The object name.</param>
public sealed record QualifiedName(Identifier? Schema, Identifier Name) : NsqlNode;