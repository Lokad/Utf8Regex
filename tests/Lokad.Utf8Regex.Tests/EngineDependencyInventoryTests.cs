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
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(static path => File.ReadLines(path).Select((line, index) => (Path: path, Line: line, Number: index + 1)))
            .Where(static item => item.Line.Contains("new Regex(", StringComparison.Ordinal))
            .Select(static item => $"{GetRelativePath(item.Path)}:{item.Number}: {item.Line.Trim()}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        // R05 baseline (2026-09-22, after PCRE2 managed-plumbing deletion): 3 construction sites.
        // - Utf8FallbackVerifierPlan.cs x2: core fallbackRegex + anchoredFallbackRegex.
        //   Purpose: candidate verification, IsMatch/Match/Count/Replace fallback, group metadata.
        //   Replacement owner: R03 (core engine independence).
        // - Utf8PythonRegex.Managed.cs x1: lazy _managedRegex construction (R04: was unconditional).
        //   Purpose: PythonRe managed-backend lanes only; utf8-backed lanes never construct it.
        //   Replacement owner: R04 (PythonRe migration).
        // Former ValidateOptions probe in Utf8Regex.cs is owned since R00 and must not regress.
        // Former PCRE2 managed-compat probe in Utf8Pcre2Regex.cs is deleted since R05 and must not regress.
        Assert.Equal(3, hits.Length);
        Assert.Equal(2, hits.Count(static hit => hit.Contains("Utf8FallbackVerifierPlan.cs", StringComparison.Ordinal)));
        Assert.Equal(1, hits.Count(static hit => hit.Contains("Utf8PythonRegex.Managed.cs", StringComparison.Ordinal)));
        Assert.DoesNotContain(hits, static hit => hit.Contains("Utf8Pcre2Regex.cs", StringComparison.Ordinal));
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
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
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

        // 2. BCL enumerator structs: split enumeration and operation cursors (PCRE2 managed lanes deleted in R05).
        //    Owners: R03 (core) and R05 (PCRE2 managed-lane deletion).
        Assert.Equal(new[] { "Utf8ValueSplitEnumerator.cs" }, FilesContaining("Regex.ValueSplitEnumerator"));
        Assert.Equal(new[] { "Utf8OperationMatchCursor.cs" }, FilesContaining("Regex.ValueMatchEnumerator"));

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

        // 4. BCL Match chaining via NextMatch: core evaluator loop and PythonRe FindIter (PCRE2 global loop deleted in R05).
        //    Owners: R03 (core) and R04 (adapters).
        Assert.Equal(new[] { "Utf8PythonRegex.FindIter.cs", "Utf8Regex.cs" }, FilesContaining(".NextMatch()"));

        // 5. BCL result plumbing holders. Owner: R02/R03 (owned match/group/capture storage).
        Assert.Equal(new[] { "Utf8MatchContext.cs" }, FilesContaining("Match? _match"));
        Assert.Equal(new[] { "Utf8GroupContext.cs" }, FilesContaining("Group? _group"));
        Assert.Equal(new[] { "Utf8CaptureContext.cs" }, FilesContaining("Capture? _capture"));

        // 6. Regex.Escape is a pure string helper used while building translated patterns.
        Assert.Equal(new[] { "Pcre2ExecutionArchitecture.cs", "PythonReParser.cs", "PythonReTranslator.cs" }, FilesContaining("Regex.Escape("));
    }

    [Fact]
    public void AdapterIndirectCoreRoutesStayInAllowlistedFiles()
    {
        // R00 indirect-route inventory: adapters reach core execution only through the
        // reviewed ByteOffsetExecution integration surface (PCRE2) and the _utf8Regex
        // fast-path backend (PythonRe). Replacement owner: R04 (adapter migration).
        // Core itself names PythonRe only on PYTHONRE-INTEGRATION-POINT lines (the PCRE2
        // half is guarded by Pcre2SourceGuardTests in the PCRE2 test project).
        var repoRoot = FindRepositoryRoot();
        var srcRoot = Path.Combine(repoRoot, "src");

        string[] NamesIn(string directory, params string[] tokens)
        {
            return Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(path => File.ReadLines(path).Any(line => tokens.Any(token => line.Contains(token, StringComparison.Ordinal))))
                .Select(path => Path.GetFileName(path))
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        var pcre2Directory = Path.Combine(srcRoot, "Lokad.Utf8Regex.Pcre2");
        Assert.Equal(
            new[] { "Pcre2ExecutionArchitecture.cs", "Utf8Pcre2Regex.Diagnostics.cs", "Utf8Pcre2Regex.cs" },
            NamesIn(pcre2Directory, "ByteOffsetExecution"));
        Assert.Equal(
            new[] { "Pcre2ExecutionArchitecture.cs", "Utf8Pcre2Regex.cs" },
            NamesIn(pcre2Directory, "new Utf8Regex("));

        var pythonDirectory = Path.Combine(srcRoot, "Lokad.Utf8Regex.PythonRe");
        Assert.Equal(
            new[] { "Utf8PythonRegex.Detailed.cs", "Utf8PythonRegex.FindAll.cs", "Utf8PythonRegex.FindIter.cs", "Utf8PythonRegex.Split.cs", "Utf8PythonRegex.Substitution.cs", "Utf8PythonRegex.Utf8.cs", "Utf8PythonRegex.Value.cs", "Utf8PythonRegex.cs" },
            NamesIn(pythonDirectory, "_utf8Regex", "new Utf8Regex("));

        var coreDirectory = Path.Combine(srcRoot, "Lokad.Utf8Regex");
        var unmarked = Directory.EnumerateFiles(coreDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (Path: path, Line: line, Number: index + 1)))
            .Where(item => item.Line.Contains("PythonRe", StringComparison.Ordinal) && !item.Line.Contains("PYTHONRE-INTEGRATION-POINT", StringComparison.Ordinal))
            .Select(item => $"{Path.GetFileName(item.Path)}:{item.Number}: {item.Line.Trim()}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Empty(unmarked);
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

