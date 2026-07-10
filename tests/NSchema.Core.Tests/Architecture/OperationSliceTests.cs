using System.Reflection;
using NSchema.Diagnostics;
using NSchema.Operations;

namespace NSchema.Tests.Architecture;

/// <summary>
/// Guards the operation-slice conventions: every handler is an internal sealed
/// <c>{Name}Operation</c> implementing <c>IOperation&lt;{Name}Arguments, Result&lt;{Name}Result&gt;&gt;</c>
/// with public record arguments/result, reached through a matching <see cref="INSchemaOperations"/> method.
/// </summary>
public sealed class OperationSliceTests
{
    private static List<(Type Handler, Type Arguments, Type Result)> Handlers()
    {
        var slices = new List<(Type, Type, Type)>();
        foreach (var type in ArchitectureTestSupport.CoreAssembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
        {
            var contract = type.GetInterfaces()
                .SingleOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IOperation<,>));
            if (contract is not null)
            {
                slices.Add((type, contract.GetGenericArguments()[0], contract.GetGenericArguments()[1]));
            }
        }
        return slices;
    }

    [Fact]
    public void Handlers_AreInternalSealed_AndNamedOperation()
    {
        // Arrange
        var handlers = Handlers();

        // Act
        var offenders = handlers
            .Where(h => h.Handler.IsPublic || !h.Handler.IsSealed || !h.Handler.Name.EndsWith("Operation", StringComparison.Ordinal))
            .Select(h => h.Handler.FullName)
            .ToList();

        // Assert
        handlers.ShouldNotBeEmpty();
        offenders.ShouldBeEmpty($"Handlers breaking the internal/sealed/{{Name}}Operation convention: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void Slices_UsePublicArgumentsAndResultRecords_NamedForTheOperation()
    {
        // Arrange
        var failures = new List<string>();

        foreach (var (handler, arguments, result) in Handlers())
        {
            var name = handler.Name[..^"Operation".Length];

            // Act
            if (arguments.Name != $"{name}Arguments" || !arguments.IsPublic || !IsRecord(arguments))
            {
                failures.Add($"{handler.Name}: expected public record {name}Arguments, found {arguments.Name}");
            }

            if (!result.IsGenericType || result.GetGenericTypeDefinition() != typeof(Result<>))
            {
                failures.Add($"{handler.Name}: expected Result<{name}Result>, found {result.Name}");
                continue;
            }

            var payload = result.GetGenericArguments()[0];
            if (payload.Name != $"{name}Result" || !payload.IsPublic || !IsRecord(payload))
            {
                failures.Add($"{handler.Name}: expected public record {name}Result, found {payload.Name}");
            }
        }

        // Assert
        failures.ShouldBeEmpty($"Slice convention failures: {string.Join("; ", failures)}");
    }

    [Fact]
    public void EveryHandler_IsExposedOnTheOperationsFacade()
    {
        // Arrange
        var facadeMethods = typeof(INSchemaOperations).GetMethods();

        // Act
        var missing = Handlers()
            .Where(h => !facadeMethods.Any(m =>
                m.Name == h.Handler.Name[..^"Operation".Length] &&
                m.GetParameters().Any(p => p.ParameterType == h.Arguments)))
            .Select(h => h.Handler.FullName)
            .ToList();

        // Assert
        missing.ShouldBeEmpty($"Handlers without a matching INSchemaOperations method taking their arguments record: {string.Join(", ", missing)}");
    }

    // Every record class declares its own EqualityContract override; nothing else does.
    private static bool IsRecord(Type type) =>
        type.GetProperty("EqualityContract", BindingFlags.Instance | BindingFlags.NonPublic) is not null;
}
