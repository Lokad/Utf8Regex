using Lokad.Utf8Regex.Internal.Execution;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ExecutionDeadlineTests
{
    [Fact]
    public void InfiniteDeadlineIsExplicitAndNeverExpires()
    {
        var deadline = Utf8ExecutionDeadline.Infinite;

        Assert.True(deadline.IsInfinite);
        for (var i = 0; i < 10_000; i++)
        {
            Assert.False(deadline.Poll());
        }
    }

    [Fact]
    public void FiniteDeadlineReportsExpirationWithoutThrowing()
    {
        var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromTicks(1));
        var expired = false;
        Thread.Sleep(20);

        for (var i = 0; i < 10_000 && !expired; i++)
        {
            expired = deadline.Poll();
        }

        Assert.False(deadline.IsInfinite);
        Assert.True(expired);
    }

    [Fact]
    public void InfiniteDeadlineUsesAStableAllocationFreeState()
    {
        Assert.Same(
            Utf8ExecutionDeadline.Infinite,
            Utf8ExecutionDeadline.Start(Regex.InfiniteMatchTimeout));
    }

    [Fact]
    public void CoreDeadlineRemainsFlavorNeutralAndNonnullable()
    {
        var sourceRoot = FindCoreSourceDirectory();
        var deadlineSource = File.ReadAllText(Path.Combine(
            sourceRoot,
            "Internal",
            "Execution",
            "Utf8ExecutionDeadline.cs"));

        Assert.DoesNotContain("RegexMatchTimeoutException", deadlineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Encoding.", deadlineSource, StringComparison.Ordinal);

        foreach (var path in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("Utf8ExecutionBudget", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Utf8ExecutionDeadline?", source, StringComparison.Ordinal);
        }
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
