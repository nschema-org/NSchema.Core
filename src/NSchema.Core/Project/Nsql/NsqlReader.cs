namespace NSchema.Project.Nsql;

/// <summary>
/// Reads NSchema source into the syntax tree.
/// </summary>
public static class NsqlReader
{
    /// <summary>
    /// Reads raw <paramref name="source"/> into an <see cref="NsqlDocument"/>. The parser recovers at
    /// statement boundaries, so every syntax error is reported at once; the document carries the statements
    /// that parsed, even on a failure.
    /// </summary>
    /// <param name="source">The NSchema source text.</param>
    public static Result<NsqlDocument, NsqlDiagnostic> Read(string source)
    {
        var parser = new NsqlParser(source);
        var document = parser.Parse();
        return Result<NsqlDocument, NsqlDiagnostic>.From(document, parser.Errors.Select(NsqlDiagnostics.Syntax).ToList());
    }

    /// <summary>
    /// Reads the file at <paramref name="path"/> into an <see cref="NsqlDocument"/>, stamping the path onto
    /// the document and every diagnostic. An unreadable file is an error diagnostic, not an exception.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static async Task<Result<NsqlDocument, NsqlDiagnostic>> ReadFile(string path, CancellationToken cancellationToken = default)
    {
        string source;
        try
        {
            source = await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result<NsqlDocument, NsqlDiagnostic>.Failure(NsqlDiagnostics.UnreadableFile(path, exception));
        }

        var result = Read(source);
        return Result<NsqlDocument, NsqlDiagnostic>.From(
            result.Value is { } document ? document with { FilePath = path } : null,
            [..result.Diagnostics.Select(d => d with { File = path })]
        );
    }

    /// <summary>
    /// Reads raw <paramref name="source"/> under the configuration grammar: a typed list of configuration
    /// statements, with a project statement rejected as a syntax error (a file is one grammar or the other).
    /// </summary>
    /// <param name="source">The configuration source text.</param>
    public static Result<NsqlConfigDocument, NsqlDiagnostic> ReadConfig(string source)
    {
        var parser = new NsqlParser(source);
        var document = parser.ParseConfig();
        return Result<NsqlConfigDocument, NsqlDiagnostic>.From(document, [..parser.Errors.Select(NsqlDiagnostics.Syntax)]);
    }

    /// <summary>
    /// Reads the configuration file at <paramref name="path"/>, stamping the path onto the document and every
    /// diagnostic. An unreadable file is an error diagnostic, not an exception.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static async Task<Result<NsqlConfigDocument, NsqlDiagnostic>> ReadConfigFile(string path, CancellationToken cancellationToken = default)
    {
        string source;
        try
        {
            source = await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result<NsqlConfigDocument, NsqlDiagnostic>.Failure(NsqlDiagnostics.UnreadableFile(path, exception));
        }

        var result = ReadConfig(source);
        return Result<NsqlConfigDocument, NsqlDiagnostic>.From(
            result.Value is { } document ? document with { FilePath = path } : null,
            [..result.Diagnostics.Select(d => d with { File = path })]
        );
    }
}
