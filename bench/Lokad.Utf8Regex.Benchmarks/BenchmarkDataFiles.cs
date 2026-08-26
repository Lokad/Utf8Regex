namespace Lokad.Utf8Regex.Benchmarks;

internal static class BenchmarkDataFiles
{
    public static string GetDirectory(string relativePath)
    {
        var outputCandidate = Path.Combine(
            AppContext.BaseDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (Directory.Exists(outputCandidate))
        {
            return outputCandidate;
        }

        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null)
        {
            var sourceCandidate = Path.Combine(
                directory.FullName,
                "bench",
                "Lokad.Utf8Regex.Benchmarks",
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(sourceCandidate))
            {
                return sourceCandidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Benchmark data directory '{relativePath}' was not found beside the executable or in the repository.");
    }
}
