using NSchema.Model;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Scripts;

/// <summary>
/// <c>SCRIPT name RUN [ALWAYS|ONCE] ON event [(options)] AS $$ body $$;</c>
/// </summary>
/// <param name="Name">The script name.</param>
/// <param name="RunCondition">The <c>RUN</c> condition, or <see langword="null"/> for the bare <c>RUN</c> of a change-event script.</param>
/// <param name="Event">The <c>ON</c> event clause.</param>
/// <param name="Body">The body text, delimiters stripped.</param>
/// <param name="RunOutsideTransaction">Whether <c>run_outside_transaction = true</c> is written.</param>
public sealed record ScriptStatement(
    Identifier Name,
    RunCondition? RunCondition,
    ScriptEventClause Event,
    SqlText Body,
    bool RunOutsideTransaction = false
) : NsqlStatement
{
    /// <summary>
    /// The <c>RUN</c> condition.
    /// </summary>
    public bool HasMisplacedRunCondition => RunCondition is not null && Event is not DeploymentEventClause;

    /// <summary>
    /// The <c>SCRIPT</c> keyword token, when parsed.
    /// </summary>
    public Token? ScriptKeyword { get; init; }

    /// <summary>
    /// The <c>RUN</c> keyword token, when parsed.
    /// </summary>
    public Token? RunKeyword { get; init; }

    /// <summary>
    /// The <c>ALWAYS</c>/<c>ONCE</c> keyword token, when parsed with a condition.
    /// </summary>
    public Token? ConditionKeyword { get; init; }

    /// <summary>
    /// The <c>ON</c> keyword token, when parsed.
    /// </summary>
    public Token? OnKeyword { get; init; }

    /// <summary>
    /// The <c>(</c> token opening the options, when parsed with options.
    /// </summary>
    public Token? OptionsOpenParenToken { get; init; }

    /// <summary>
    /// The verbatim options-interior span token, when parsed with options.
    /// </summary>
    public Token? OptionsInteriorToken { get; init; }

    /// <summary>
    /// The <c>)</c> token closing the options, when parsed with options.
    /// </summary>
    public Token? OptionsCloseParenToken { get; init; }

    /// <summary>
    /// The <c>AS</c> keyword token, when parsed.
    /// </summary>
    public Token? AsKeyword { get; init; }

    /// <summary>
    /// The dollar-quoted body token, when parsed.
    /// </summary>
    public Token? BodyToken { get; init; }

    /// <summary>
    /// The terminating <c>;</c> token, when parsed.
    /// </summary>
    public Token? SemicolonToken { get; init; }

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (DocComment is { } doc)
            {
                yield return doc;
            }
            if (ScriptKeyword is { } script)
            {
                yield return script;
            }
            yield return Name;
            if (RunKeyword is { } run)
            {
                yield return run;
            }
            if (ConditionKeyword is { } condition)
            {
                yield return condition;
            }
            if (OnKeyword is { } on)
            {
                yield return on;
            }
            yield return Event;
            if (OptionsOpenParenToken is { } optionsOpen)
            {
                yield return optionsOpen;
            }
            if (OptionsInteriorToken is { } optionsInterior)
            {
                yield return optionsInterior;
            }
            if (OptionsCloseParenToken is { } optionsClose)
            {
                yield return optionsClose;
            }
            if (AsKeyword is { } asKeyword)
            {
                yield return asKeyword;
            }
            if (BodyToken is { } body)
            {
                yield return body;
            }
            if (SemicolonToken is { } semicolon)
            {
                yield return semicolon;
            }
        }
    }
}
