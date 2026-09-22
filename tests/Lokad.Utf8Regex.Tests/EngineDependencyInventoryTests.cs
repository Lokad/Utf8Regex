using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// R00 inventory: every production <c>new Regex(</c> construction has an identified
/// purpose and replacement task. Compatibility-type references (RegexOptions,
/// Regex.InfiniteMatchTimeout, RegexMatchTimeoutException) are not engine uses and
/// are listed separately from executable construction below.
/// </summary>
public sealed class EngineDependencyInventoryTests
{
    [Fact]
    public void ProductionRegexConstructionInventoryMatchesR00Baseline()
    {
        var repoRoot = FindRepositoryRoot();
        var srcRoot = Path.Combine(repoRoot, "src");
        var hits = Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(static path => File.ReadLines(path).Select((line, index) => (Path: path, Line: line, Number: index + 1)))
            .Where(static item => item.Line.Contains("new Regex(", StringComparison.Ordinal))
            .Select(static item => $"{GetRelativePath(item.Path)}:{item.Number}: {item.Line.Trim()}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        // R00 baseline (2026-09-22, after owned option validation): 4 construction sites.
        // - Utf8FallbackVerifierPlan.cs x2: core fallbackRegex + anchoredFallbackRegex.
        //   Purpose: candidate verification, IsMatch/Match/Count/Replace fallback, group metadata.
        //   Replacement owner: R03 (core engine independence).
        // - Utf8Pcre2Regex.cs x1: PCRE2 managed-compat probe.
        //   Purpose: compatibility fallback for PCRE2 profiles outside the owned PCRE2 VM.
        //   Replacement owner: R04 (PCRE2 migration).
        // - Utf8PythonRegex.Managed.cs x1: unconditional _managedRegex construction.
        //   Purpose: all PythonRe search/match/iteration/split/substitution execution.
        //   Replacement owner: R04 (PythonRe migration).
        // Former ValidateOptions probe in Utf8Regex.cs is owned since R00 and must not regress.
        Assert.Equal(4, hits.Length);
        Assert.Equal(2, hits.Count(static hit => hit.Contains("Utf8FallbackVerifierPlan.cs", StringComparison.Ordinal)));
        Assert.Equal(1, hits.Count(static hit => hit.Contains("Utf8Pcre2Regex.cs", StringComparison.Ordinal)));
        Assert.Equal(1, hits.Count(static hit => hit.Contains("Utf8PythonRegex.Managed.cs", StringComparison.Ordinal)));
        Assert.DoesNotContain(hits, static hit => hit.Contains("Utf8Regex.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(hits, static hit => hit.Contains("Utf8RegexSyntax.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void OwnedOptionValidationMatchesBclScope()
    {
        // Spot-checks beyond the DiffTests matrix: owned validation must accept BCL-valid
        // scopes and reject BCL-invalid scopes without constructing a Regex engine.
        // Valid scopes (BCL accepts): plain, ECMAScript-family, NonBacktracking-family.
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.ECMAScript,
            RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant,
            RegexOptions.NonBacktracking,
            RegexOptions.NonBacktracking | RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var utf8Regex = new Utf8Regex("foo", options);
            var runtimeRegex = new Regex("foo", options);
            Assert.Equal(runtimeRegex.Options, utf8Regex.Options);
        }

        foreach (var options in new RegexOptions[]
        {
            (RegexOptions)0x0800,
            RegexOptions.ECMAScript | RegexOptions.Singleline,
            RegexOptions.NonBacktracking | RegexOptions.RightToLeft,
        })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Regex("foo", options));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Regex("foo", options));
        }
    }

    private static string GetRelativePath(string path)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "Lokad.Utf8Regex")))
            {
                return Path.GetRelativePath(directory.FullName, path);
            }

            directory = directory.Parent;
        }

        return path;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "Lokad.Utf8Regex")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}

