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

    [Fact]
    public void ProductionExecutableEngineUsesStayInAllowlistedFiles()
    {
        // R00 executable-use inventory: every production BCL engine member access has an
        // identified purpose and replacement owner. Compatibility-only references (RegexOptions,
        // Regex.InfiniteMatchTimeout, RegexMatchTimeoutException and the Regex.Escape string
        // helper) are pinned separately and are not engine execution.
        var repoRoot = FindRepositoryRoot();
        var srcRoot = Path.Combine(repoRoot, "src");

        string[] FilesContaining(string token)
        {
            return Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(path => File.ReadLines(path).Any(line => line.Contains(token, StringComparison.Ordinal)))
                .Select(path => Path.GetFileName(path))
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        string[] FilesContainingAny(params string[] tokens)
        {
            return Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(path => File.ReadLines(path).Any(line => tokens.Any(token => line.Contains(token, StringComparison.Ordinal))))
                .Select(path => Path.GetFileName(path))
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        // 1. Core fallback execution (union of the three receiver spellings below).
        //    Purpose: candidate verification plus all uncovered-pattern routes. Owner: R03.
        var fallbackFiles = FilesContainingAny("FallbackCandidateVerifier.FallbackRegex", "FallbackRegex.", "fallbackRegex.Count(");
        var expectedFallbackFiles = new[]
        {
            "Utf8AsciiCultureInvariantReplacement.cs",
            "Utf8AsciiCultureInvariantStrategy.cs",
            "Utf8CompiledEngineRuntime.cs",
            "Utf8CompiledOperationCursorFactory.cs",
            "Utf8FallbackCandidateVerifier.cs",
            "Utf8QuotedLineSegmentExecutor.cs",
            "Utf8Regex.Adapters.cs",
            "Utf8Regex.Compiled.cs",
            "Utf8Regex.cs",
            "Utf8Regex.Diagnostics.cs",
            "Utf8Regex.Fallback.cs",
            "Utf8Regex.Replacement.cs",
            "Utf8RegexProgram.cs",
            "Utf8StructuralLinearCompiledRouter.cs",
            "Utf8StructuralLinearProgram.cs",
            "Utf8UnicodeLetterBoundedRepeatExecutor.cs",
        };
        Assert.Equal(expectedFallbackFiles.Order(StringComparer.Ordinal).ToArray(), fallbackFiles);

        // 2. BCL enumerator structs: split enumeration, operation cursors, PCRE2 managed lanes.
        //    Owners: R03 (core) and R04 (PCRE2 managed-lane migration).
        Assert.Equal(new[] { "Utf8ValueSplitEnumerator.cs" }, FilesContaining("Regex.ValueSplitEnumerator"));
        Assert.Equal(new[] { "Utf8OperationMatchCursor.cs", "Utf8Pcre2CoreTypes.cs" }, FilesContaining("Regex.ValueMatchEnumerator"));

        // 3. PythonRe managed execution lives only in the PythonRe adapter. Core must never see it.
        //    Purpose: translated search, full-match, iteration, split, substitution. Owner: R04.
        var managedHits = Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => File.ReadLines(path).Any(line => line.Contains("_managedRegex", StringComparison.Ordinal)))
            .ToArray();
        Assert.NotEmpty(managedHits);
        foreach (var file in managedHits)
        {
            Assert.Contains("Lokad.Utf8Regex.PythonRe", file, StringComparison.Ordinal);
        }

        // 4. BCL Match chaining via NextMatch: core evaluator loop, PCRE2 global loop, PythonRe FindIter.
        //    Owners: R03 (core) and R04 (adapters).
        Assert.Equal(new[] { "Utf8Pcre2Regex.cs", "Utf8PythonRegex.FindIter.cs", "Utf8Regex.cs" }, FilesContaining(".NextMatch()"));

        // 5. BCL result plumbing holders. Owner: R02/R03 (owned match/group/capture storage).
        Assert.Equal(new[] { "Utf8MatchContext.cs" }, FilesContaining("Match? _match"));
        Assert.Equal(new[] { "Utf8GroupContext.cs" }, FilesContaining("Group? _group"));
        Assert.Equal(new[] { "Utf8CaptureContext.cs" }, FilesContaining("Capture? _capture"));

        // 6. Regex.Escape is a pure string helper used while building translated patterns.
        Assert.Equal(new[] { "Pcre2ExecutionArchitecture.cs", "PythonReParser.cs", "PythonReTranslator.cs" }, FilesContaining("Regex.Escape("));
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

