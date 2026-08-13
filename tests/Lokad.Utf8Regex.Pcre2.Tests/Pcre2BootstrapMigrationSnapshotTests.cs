using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2BootstrapMigrationSnapshotTests
{
    private static readonly Regex s_enumMember = new(
        @"^\s*(?<name>[A-Za-z][A-Za-z0-9]*)\s*=\s*\d+,?\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Multiline);

    [Fact]
    public void MigrationLedgerAccountsForEveryBootstrapExecutionKindAndClassifierArm()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "Lokad.Utf8Regex.Pcre2", "Utf8Pcre2Regex.cs"));
        var ledgerLines = File.ReadAllLines(FindRepositoryFile("tests", "Lokad.Utf8Regex.Pcre2.Tests", "BootstrapMigration.Shipped.txt"));
        var executionKinds = ParseExecutionKinds(source);
        var executionKindLines = ledgerLines
            .TakeWhile(static line => !line.StartsWith("# Complete-pattern", StringComparison.Ordinal))
            .Where(static line => line.Length != 0 && line[0] != '#' && line.Contains('|', StringComparison.Ordinal))
            .ToArray();
        var ledgerKinds = executionKindLines
            .Select(static line => line.Split('|', StringSplitOptions.TrimEntries)[0])
            .ToArray();

        Assert.Equal(executionKinds, ledgerKinds);
        Assert.All(
            executionKindLines,
            static line => Assert.Equal(3, line.Split('|', StringSplitOptions.TrimEntries).Length));

        var classifier = ExtractClassifier(source);
        var classifierArmCount = classifier.Split('\n').Count(static line => line.Contains("=> Pcre2ExecutionKind.", StringComparison.Ordinal));
        var classifierHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(classifier)));
        Assert.Contains($"classifier-arms = {classifierArmCount}", ledgerLines, StringComparer.Ordinal);
        Assert.Contains($"classifier-sha256 = {classifierHash}", ledgerLines, StringComparer.Ordinal);
    }

    private static string[] ParseExecutionKinds(string source)
    {
        const string enumStartToken = "    internal enum Pcre2ExecutionKind";
        const string enumEndToken = "    private enum TrailingAssertionKind";
        var start = source.IndexOf(enumStartToken, StringComparison.Ordinal);
        var end = source.IndexOf(enumEndToken, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "Could not locate Pcre2ExecutionKind in the bootstrap source.");
        return s_enumMember.Matches(source[start..end])
            .Select(static match => match.Groups["name"].Value)
            .ToArray();
    }

    private static string ExtractClassifier(string source)
    {
        const string methodStartToken = "    private static Pcre2ExecutionKind ClassifyExecutionKind";
        const string methodEndToken = "    private static Pcre2NameEntry[] GetPatternNameEntries";
        var start = source.IndexOf(methodStartToken, StringComparison.Ordinal);
        var end = source.IndexOf(methodEndToken, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "Could not locate ClassifyExecutionKind in the bootstrap source.");
        return source[start..end].ReplaceLineEndings("\n").Trim();
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidateSegments = new string[segments.Length + 1];
            candidateSegments[0] = directory.FullName;
            segments.CopyTo(candidateSegments, 1);
            var candidate = Path.Combine(candidateSegments);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate repository file '{Path.Combine(segments)}'.");
    }
}
