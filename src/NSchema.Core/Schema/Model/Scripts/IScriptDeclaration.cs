namespace NSchema.Schema.Model.Scripts;

/// <summary>
/// The capability shared by every declared script, whichever event it runs on: an identity, a SQL body, and a
/// run condition. Lets machinery that treats deployment scripts and data migrations identically (run-once
/// resolution, execution hashing, name validation) be written once, while the per-kind models stay separate
/// until the unified script model lands in 5.0.
/// </summary>
internal interface IScriptDeclaration
{
    /// <summary>The script's declared name, or <see langword="null"/> for a legacy anonymous migration.</summary>
    string? Name { get; }

    /// <summary>The script's SQL body.</summary>
    string Sql { get; }

    /// <summary>The canonical hash of the SQL body.</summary>
    string Hash { get; }

    /// <summary>When the script runs, relative to occurrences of its event.</summary>
    RunCondition RunCondition { get; }
}
