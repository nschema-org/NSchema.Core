using System.Text.RegularExpressions;

namespace NSchema.Tests.Diagnostics;

/// <summary>
/// The set of findings NSchema can report, and the code each is addressed by.
/// </summary>
/// <remarks>
/// A code keys a finding in configuration and documentation, so it is a contract: this snapshot makes adding,
/// renaming or removing one a deliberate, reviewable change. The codes are literals inside catalogue members, so
/// they are read from the sources — nothing can enumerate them at runtime without invoking every member.
/// </remarks>
public sealed class DiagnosticCatalogueTests
{
    private static readonly Regex Member = new("""public static (?:Diagnostic|NsqlDiagnostic) (?<member>\w+)""");

    private static readonly Regex Code = new("\"(?<code>[a-z0-9]+(?:-[a-z0-9]+)*)\"");

    private static List<(string Catalogue, string Member, string Code)> Catalogue()
    {
        var findings = new List<(string, string, string)>();
        foreach (var file in Directory.EnumerateFiles(SourceRoot(), "*Diagnostics.cs", SearchOption.AllDirectories))
        {
            var catalogue = Path.GetFileNameWithoutExtension(file);
            string? member = null;
            var claimed = new HashSet<string>();
            foreach (var line in File.ReadLines(file))
            {
                if (Member.Match(line) is { Success: true } declaration)
                {
                    member = declaration.Groups["member"].Value;
                }

                // The first code after a declaration is that member's; a member worded two ways repeats it.
                if (member is null || !claimed.Add(member))
                {
                    continue;
                }

                if (Code.Match(line) is { Success: true } code)
                {
                    findings.Add((catalogue, member, code.Groups["code"].Value));
                }
                else
                {
                    claimed.Remove(member);
                }
            }
        }

        return [.. findings.OrderBy(f => f.Item1, StringComparer.Ordinal).ThenBy(f => f.Item2, StringComparer.Ordinal)];
    }

    [Fact]
    public Task Catalogue_ListsEveryFindingAndItsCode() =>
        Verify(string.Join("\n", Catalogue().Select(f => $"{f.Code,-42} {f.Catalogue}.{f.Member}")));

    [Fact]
    public void EveryCodeIsUnique()
    {
        // A code addresses one finding, so a repeat would make two impossible to configure apart.
        var duplicates = Catalogue()
            .GroupBy(f => f.Code)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(f => $"{f.Catalogue}.{f.Member}"))}")
            .ToList();

        duplicates.ShouldBeEmpty();
    }

    private static string SourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root."),
            "src");
    }
}
