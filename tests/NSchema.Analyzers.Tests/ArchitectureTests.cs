namespace NSchema.Analyzers.Tests;

/// <summary>
/// The layering table's own integrity. The analyzers prove the code obeys the table; nothing they can see proves
/// the table is worth obeying, so that is checked here.
/// </summary>
public sealed class ArchitectureTests
{
    public static TheoryData<string> Parts => [.. Architecture.Dependencies.Keys];

    /// <summary>
    /// A table that let two parts list each other would permit a cycle by decree, and every reference between them
    /// would pass.
    /// </summary>
    [Theory]
    [MemberData(nameof(Parts))]
    public void Layering_IsAcyclic(string part)
    {
        // Arrange
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>(Architecture.AllowedFor(part));

        // Act
        while (pending.TryDequeue(out var next))
        {
            if (reachable.Add(next))
            {
                foreach (var onward in Architecture.AllowedFor(next))
                {
                    pending.Enqueue(onward);
                }
            }
        }

        // Assert
        reachable.ShouldNotContain(part, $"'{part}' can reach itself through the declared layering");
    }

    /// <summary>
    /// A row naming a part that does not exist is an edge to nowhere, which no rule would ever report on.
    /// </summary>
    [Theory]
    [MemberData(nameof(Parts))]
    public void Layering_NamesOnlyDeclaredParts(string part)
    {
        // Act
        var unknown = Architecture.Dependencies[part].Except(Architecture.Slices);

        // Assert
        unknown.ShouldBeEmpty($"'{part}' declares a dependency on something that is not a part");
    }

    [Fact]
    public void Kernel_IsDeclaredInTheLayering()
    {
        // Act
        var missing = Architecture.Kernel.Except(Architecture.Dependencies.Keys);

        // Assert
        missing.ShouldBeEmpty("a kernel part still needs a row saying what it may use");
    }

    /// <summary>
    /// The table keeps up with the code. NS0004 catches a part the table has not heard of; this catches the other
    /// direction — a row left behind after its folder was renamed away, which governs nothing while looking as
    /// though it governs a slice.
    /// </summary>
    [Theory]
    [MemberData(nameof(Parts))]
    public void DeclaredPart_ExistsInTheEngine(string part)
    {
        // Act
        var namespaces = typeof(NSchemaApplication).Assembly.GetTypes()
            .Select(type => type.Namespace)
            .Where(space => space is not null && space.StartsWith("NSchema.", StringComparison.Ordinal))
            .Select(space => space!.Split('.')[1]);

        // Assert
        namespaces.ShouldContain(part, $"'{part}' has a row in the layering table but no code");
    }
}
