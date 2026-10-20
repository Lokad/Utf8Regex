namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2BootstrapMigrationSnapshotTests
{
    [Fact]
    public void GenericCompilerAndRunnerOwnAllSupportedExecution()
    {
        var sourceDirectory = Path.GetDirectoryName(FindRepositoryFile("src", "Lokad.Utf8Regex.Pcre2", "Utf8Pcre2Regex.cs"))!;
        var source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                    !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));

        Assert.DoesNotContain("Pcre2ExecutionKind", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ClassifyExecutionKind", source, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(Pattern", source, StringComparison.Ordinal);
        Assert.DoesNotContain("switch (Pattern)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Pattern switch", source, StringComparison.Ordinal);
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
