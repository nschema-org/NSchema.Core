using NSchema.Migration;
using NSchema.Operations;
using NSchema.Operations.Services;
using NSchema.Schema.Model;

namespace NSchema.Tests.Operations;

public sealed class ValidateOperationTests
{
    private readonly IMigrationHelper _helper = Substitute.For<IMigrationHelper>();
    private readonly IMigrationReporter _reporter = Substitute.For<IMigrationReporter>();

    private ValidateOperation BuildSut() => new(_helper, Helpers.TestReporters.ResolverFor(_reporter));

    private readonly ValidateOperation _sut;

    public ValidateOperationTests()
    {
        _helper.Validate(Arg.Any<CancellationToken>()).Returns(DatabaseSchema.Create([]));
        _sut = BuildSut();
    }

    [Fact]
    public async Task Execute_DelegatesToHelperValidateDesiredSchema()
    {
        // Act
        await _sut.Execute(TestContext.Current.CancellationToken);

        // Assert
        await _helper.Received(1).Validate(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ValidationThrows_Propagates()
    {
        // Arrange
        _helper.Validate(Arg.Any<CancellationToken>())
            .Returns<DatabaseSchema>(_ => throw new InvalidOperationException("boom"));

        // Act
        var act = () => _sut.Execute();

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(act);
    }
}
