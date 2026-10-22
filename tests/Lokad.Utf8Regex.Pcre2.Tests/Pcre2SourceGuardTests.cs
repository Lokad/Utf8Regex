using System.Reflection;
using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Pcre2;
using Lokad.Utf8Regex.Tests.Common;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// This fixture owns PCRE2 isolation and semantic dependencies. Nullable and
// documentation checks also remain enforced by the shipped project builds.
public sealed class Pcre2SourceGuardTests
{
    [Fact]
    public void ConsumedPcre2ReferencesNoExternalMatchers()
    {
        AssemblyDependencyInventory.AssertNoExternalMatchers(typeof(Utf8Pcre2Regex).Assembly.Location);
    }

    [Fact]
    public void Pcre2TypeNamesCarryPcre2Ownership()
    {
        // Synthetic metadata types (e.g. __StaticArrayInitTypeSize=N) carry no declared simple name: only identifier-shaped names participate.
        var offenders = typeof(Utf8Pcre2Regex).Assembly.GetTypes()
            .Where(static type => type.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .Select(static type => type.Name)
            .Where(static name => name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_') && name.All(static c => char.IsLetterOrDigit(c) || c == '_' || c == '`'))
            .Where(static name => !name.Contains("Pcre2", StringComparison.Ordinal))
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
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

    [Fact]
    public void Pcre2CharacterSemanticsDoNotDependOnDotNetRegexInternals()
    {
        var source = File.ReadAllText(Path.Combine(FindPcre2SourceDirectory(), "Pcre2CharacterCompiler.cs"));

        Assert.DoesNotContain("System.Text.RegularExpressions", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RegexCharClass", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RegexOptions", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Pcre2BacktrackingSemanticsDoNotDependOnDotNetRegexInternals()
    {
        var source = File.ReadAllText(Path.Combine(FindPcre2SourceDirectory(), "Pcre2BacktrackingCompiler.cs"));

        Assert.DoesNotContain("System.Text.RegularExpressions", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RegexCharClass", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RegexOptions", source, StringComparison.Ordinal);
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
