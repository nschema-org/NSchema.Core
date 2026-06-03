namespace NSchema.Schema.Model;

/// <summary>
/// Defines the type of a SQL script, indicating when it should be executed in relation to the main migration actions.
/// </summary>
public enum ScriptType
{
    /// <summary>
    /// The script is executed before the main migration actions.
    /// </summary>
    PreDeployment,

    /// <summary>
    /// The script is executed after the main migration actions.
    /// </summary>
    PostDeployment,
}
