using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2SourceGuardTests
{
    private static readonly Regex s_nullForgivingExpression = new(
        @"(?<target>[A-Za-z_][A-Za-z0-9_]*)!(?=[.\[;,)])",
        RegexOptions.CultureInvariant);

    [Fact]
    public void Pcre2ContractsDoNotUseDefaultParameters()
    {
        var assembly = typeof(Utf8Pcre2Regex).Assembly;
        var offenders = assembly.GetTypes()
            .SelectMany(static type =>
                type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Cast<MethodBase>()
                    .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)))
            .SelectMany(static member => member.GetParameters().Select(parameter => (Member: member, Parameter: parameter)))
            .Where(static item => item.Parameter.HasDefaultValue || item.Parameter.IsOptional)
            .Select(static item => $"{item.Member.DeclaringType?.FullName}.{item.Member.Name}({item.Parameter.Name})")
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Pcre2OwnedSourceDoesNotGrowMechanicalCSharpDebt()
    {
        var sourceDirectory = FindPcre2SourceDirectory();
        var sourceFiles = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories);
        var source = string.Join('\n', sourceFiles.OrderBy(static path => path, StringComparer.Ordinal).Select(File.ReadAllText));

        // Frozen L0 baseline. Later owners may reduce these counts, but no change may
        // silently add a new target or increase a legacy target's use.
        var allowedNullForgivingCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["_groups"] = 1,
            ["_matches"] = 1,
        };
        var actualNullForgivingCounts = s_nullForgivingExpression.Matches(source)
            .Select(static match => match.Groups["target"].Value)
            .GroupBy(static target => target, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);

        foreach (var (target, count) in actualNullForgivingCounts)
        {
            Assert.True(
                allowedNullForgivingCounts.TryGetValue(target, out var allowedCount) && count <= allowedCount,
                $"Null-forgiving expression debt grew for '{target}': {count} occurrence(s).");
        }

        Assert.DoesNotContain(" abstract ", source, StringComparison.Ordinal);
        Assert.DoesNotContain(" virtual ", source, StringComparison.Ordinal);

        var projectFiles = Directory.GetFiles(sourceDirectory, "*.csproj", SearchOption.AllDirectories);
        foreach (var projectFile in projectFiles)
        {
            Assert.DoesNotContain("<InternalsVisibleTo", File.ReadAllText(projectFile), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CorePcre2ReferencesRemainExplicitIntegrationPoints()
    {
        var pcre2SourceDirectory = FindPcre2SourceDirectory();
        var coreSourceDirectory = Path.Combine(Directory.GetParent(pcre2SourceDirectory)?.FullName ?? throw new InvalidOperationException("PCRE2 source has no parent directory."), "Lokad.Utf8Regex");
        var offenders = Directory.GetFiles(coreSourceDirectory, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (Path: path, Line: line, Number: index + 1)))
            .Where(static item =>
                (item.Line.Contains("Pcre2", StringComparison.Ordinal) || item.Line.Contains("PCRE2", StringComparison.Ordinal)) &&
                !item.Line.Contains("PCRE2-INTEGRATION-POINT", StringComparison.Ordinal))
            .Select(static item => $"{item.Path}:{item.Number}: {item.Line.Trim()}")
            .ToArray();

        Assert.Empty(offenders);
    }

    private static string FindPcre2SourceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Lokad.Utf8Regex.Pcre2");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the owned Lokad.Utf8Regex.Pcre2 source directory.");
    }
}
