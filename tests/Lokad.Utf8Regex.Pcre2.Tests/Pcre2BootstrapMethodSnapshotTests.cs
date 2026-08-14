namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2BootstrapMethodSnapshotTests
{
    [Fact]
    public void FacadeContainsNoPatternSpecificMatchOrReplacementBootstrap()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "Lokad.Utf8Regex.Pcre2", "Utf8Pcre2Regex.cs"));

        Assert.DoesNotContain("MatchVia", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MatchDetailedVia", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplaceVia", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplacementVia", source, StringComparison.Ordinal);

        var ledger = File.ReadAllText(FindRepositoryFile("tests", "Lokad.Utf8Regex.Pcre2.Tests", "BootstrapMethods.Shipped.txt"));
        Assert.Contains("bootstrap-specific-method-count = 0", ledger, StringComparison.Ordinal);
    }

    [Fact]
    public void CompileValidatorContainsNoCompletePatternChecks()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "Lokad.Utf8Regex.Pcre2", "Pcre2CompileValidator.cs"));

        Assert.Contains("ValidateStructure(pattern)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("pattern ==", source, StringComparison.Ordinal);
        Assert.DoesNotContain("pattern is @", source, StringComparison.Ordinal);
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
