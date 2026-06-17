namespace NSchema.Operations;

/// <summary>
/// Intent-revealing convenience verbs over <see cref="IOperationReporter.Report"/>.
/// </summary>
public static class OperationReporterExtensions
{
    extension(IOperationReporter reporter)
    {
        /// <summary>
        /// Reports a neutral statement about the operation (header or neutral outcome).
        /// </summary>
        public void Announce(string message) => reporter.Report(MessageKind.Announcement, message);

        /// <summary>
        /// Reports a transient progress step.
        /// </summary>
        public void Progress(string message) => reporter.Report(MessageKind.Progress, message);

        /// <summary>
        /// Reports a successful outcome.
        /// </summary>
        public void Success(string message) => reporter.Report(MessageKind.Success, message);

        /// <summary>
        /// Reports a non-fatal warning.
        /// </summary>
        public void Warn(string message) => reporter.Report(MessageKind.Warning, message);
    }
}
