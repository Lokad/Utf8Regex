using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2BootstrapMethodSnapshotTests
{
    private static readonly Regex s_bootstrapMethod = new(
        @"(?m)^\s*private\s+(?:static\s+)?(?:[A-Za-z0-9_<>,\[\]?\.]+\s+)+(?<name>(?:Match|MatchDetailed|Probe|Replace|Enumerate|Count)[A-Za-z0-9_]*(?:Via|Global|Native|Special)[A-Za-z0-9_]*)\s*\(",
        RegexOptions.CultureInvariant);

    [Fact]
    public void BootstrapMethodDeletionMapAccountsForEveryLegacyExecutionMethod()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "Lokad.Utf8Regex.Pcre2", "Utf8Pcre2Regex.cs"));
        var actualNames = s_bootstrapMethod.Matches(source)
            .Select(static match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        var ledgerLines = File.ReadAllLines(FindRepositoryFile("tests", "Lokad.Utf8Regex.Pcre2.Tests", "BootstrapMethods.Shipped.txt"))
            .Where(static line => line.Length != 0 && line[0] != '#')
            .ToArray();
        var ledgerNames = ledgerLines.Select(static line => line.Split('|', StringSplitOptions.TrimEntries)[0]).ToArray();

        Assert.Equal(actualNames, ledgerNames);
        Assert.All(
            ledgerLines,
            static line =>
            {
                var parts = line.Split('|', StringSplitOptions.TrimEntries);
                Assert.Equal(2, parts.Length);
                Assert.Matches(@"^L(?:[3-9]|1[0-4])$", parts[1]);
            });
    }

    [Fact]
    public void BootstrapCompileValidatorIsFrozenUntilTheGenericCompilerOwnsEachCheck()
    {
        var validator = File.ReadAllText(FindRepositoryFile("src", "Lokad.Utf8Regex.Pcre2", "Pcre2CompileValidator.cs"))
            .ReplaceLineEndings("\n")
            .Trim();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(validator)));
        var migrationLedger = File.ReadAllLines(FindRepositoryFile("tests", "Lokad.Utf8Regex.Pcre2.Tests", "BootstrapMigration.Shipped.txt"));

        Assert.Contains($"validator-sha256 = {hash}", migrationLedger, StringComparer.Ordinal);
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
