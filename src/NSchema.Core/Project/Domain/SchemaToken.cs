using NSchema.Project.Domain.Models;

namespace NSchema.Project.Domain;

/// <summary>
/// The <c>{schema}</c> substitution token a template script may carry in its name and SQL body, and the
/// substitution that instantiates it for an applied schema. The single home of the token's spelling.
/// </summary>
internal static class SchemaToken
{
    public const string Token = "{schema}";

    /// <summary>
    /// Instantiates the token in a script name, producing the instance's identifier.
    /// </summary>
    public static SqlIdentifier Instantiate(SqlIdentifier name, SqlIdentifier schema) =>
        new(Instantiate(name.Value, schema));

    /// <summary>
    /// Instantiates the token in raw text (a SQL body).
    /// </summary>
    public static string Instantiate(string text, SqlIdentifier schema) =>
        text.Replace(Token, schema.Value, StringComparison.Ordinal);
}
