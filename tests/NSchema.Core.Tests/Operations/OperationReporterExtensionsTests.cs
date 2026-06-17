using NSchema.Operations;

namespace NSchema.Tests.Operations;

public sealed class OperationReporterExtensionsTests
{
    private readonly IOperationReporter _reporter = Substitute.For<IOperationReporter>();

    [Fact]
    public void Announce_ReportsAnnouncementKind()
    {
        _reporter.Announce("hi");
        _reporter.Received(1).Report(MessageKind.Announcement, "hi");
    }

    [Fact]
    public void Progress_ReportsProgressKind()
    {
        _reporter.Progress("hi");
        _reporter.Received(1).Report(MessageKind.Progress, "hi");
    }

    [Fact]
    public void Success_ReportsSuccessKind()
    {
        _reporter.Success("hi");
        _reporter.Received(1).Report(MessageKind.Success, "hi");
    }

    [Fact]
    public void Warn_ReportsWarningKind()
    {
        _reporter.Warn("hi");
        _reporter.Received(1).Report(MessageKind.Warning, "hi");
    }
}
