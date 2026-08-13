using Lokad.Utf8Regex.Internal.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8OperationCursorArchitectureTests
{
    [Fact]
    public void DiagnosticSessionsNestWithoutChangingResults()
    {
        var regex = new Utf8Regex("ab.*cd", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("ab12cd xx ab34cd");
        var expected = regex.Count(input);
        var outer = Utf8SearchDiagnosticsSession.Start();

        var outerResult = regex.Count(input);
        var inner = Utf8SearchDiagnosticsSession.Start();
        var innerResult = regex.Count(input);
        inner.Complete();

        Assert.Same(outer, Utf8SearchDiagnosticsSession.Current);
        Assert.Equal(expected, outerResult);
        Assert.Equal(expected, innerResult);
        outer.Complete();
        Assert.Null(Utf8SearchDiagnosticsSession.Current);
        Assert.Equal(expected, regex.Count(input));
    }

    [Fact]
    public void MatchAndSplitAdaptersShareTheInternalOperationCursor()
    {
        var sourceRoot = FindCoreSourceDirectory();
        var publicEnumerator = File.ReadAllText(Path.Combine(sourceRoot, "Utf8ValueMatchEnumerator.cs"));
        var splitEnumerator = File.ReadAllText(Path.Combine(sourceRoot, "Utf8ValueSplitEnumerator.cs"));
        var facade = File.ReadAllText(Path.Combine(sourceRoot, "Utf8Regex.cs"));
        var runtime = File.ReadAllText(Path.Combine(
            sourceRoot,
            "Internal",
            "Execution",
            "Utf8CompiledEngineRuntime.cs"));

        Assert.Contains("Utf8OperationMatchCursor", publicEnumerator, StringComparison.Ordinal);
        Assert.DoesNotContain("Utf8SearchExecutor", publicEnumerator, StringComparison.Ordinal);
        Assert.Contains("Utf8OperationMatchCursor", splitEnumerator, StringComparison.Ordinal);
        Assert.DoesNotContain("NativeSimplePattern", splitEnumerator, StringComparison.Ordinal);
        Assert.DoesNotContain("NativeDeterministicPattern", splitEnumerator, StringComparison.Ordinal);
        Assert.Contains("Utf8AsciiCultureInvariantStrategy", facade, StringComparison.Ordinal);
        Assert.DoesNotContain("Utf8Regex? _asciiCultureInvariantTwin", facade, StringComparison.Ordinal);
        Assert.Contains("Utf8RegexProgram _program", facade, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateMatchEnumerator", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateSplitEnumerator", runtime, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ab", "xxabyyab", false)]
    [InlineData("", "abc", false)]
    [InlineData("(a)(b)", "xxabyyab", false)]
    [InlineData("ab.*cd", "ab12cd xx ab34cd", false)]
    [InlineData("ab", "xxabyyab", true)]
    public void CompiledAndInterpretedOperationAdaptersRemainEquivalent(
        string pattern,
        string text,
        bool rightToLeft)
    {
        var baseOptions = RegexOptions.CultureInvariant |
            (rightToLeft ? RegexOptions.RightToLeft : RegexOptions.None);
        var interpreted = new Utf8Regex(pattern, baseOptions);
        var compiled = new Utf8Regex(pattern, baseOptions | RegexOptions.Compiled);
        var input = Encoding.UTF8.GetBytes(text);

        Assert.Equal(interpreted.IsMatch(input), compiled.IsMatch(input));
        Assert.Equal(interpreted.Match(input), compiled.Match(input));
        Assert.Equal(interpreted.Count(input), compiled.Count(input));
        Assert.Equal(CollectMatches(interpreted, input), CollectMatches(compiled, input));
        Assert.Equal(CollectSplits(interpreted, input), CollectSplits(compiled, input));
        Assert.Equal(interpreted.Replace(input, "<$&>"), compiled.Replace(input, "<$&>"));
    }

    private static List<Utf8ValueMatch> CollectMatches(Utf8Regex regex, byte[] input)
    {
        var matches = new List<Utf8ValueMatch>();
        foreach (var match in regex.EnumerateMatches(input))
        {
            matches.Add(match);
        }

        return matches;
    }

    private static List<(bool IsByteAligned, int IndexInUtf16, int LengthInUtf16, int IndexInBytes, int LengthInBytes, string Value)> CollectSplits(
        Utf8Regex regex,
        byte[] input)
    {
        var splits = new List<(bool, int, int, int, int, string)>();
        foreach (var split in regex.EnumerateSplits(input))
        {
            splits.Add((
                split.IsByteAligned,
                split.IndexInUtf16,
                split.LengthInUtf16,
                split.IndexInBytes,
                split.LengthInBytes,
                split.GetValueString()));
        }

        return splits;
    }

    private static string FindCoreSourceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Lokad.Utf8Regex");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Lokad.Utf8Regex source directory.");
    }
}
