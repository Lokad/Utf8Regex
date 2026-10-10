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
    public void ConsumedCoreHasNoBclRegexFactoriesOrEngineOwners()
    {
        var assembly = typeof(Utf8Regex).Assembly;
        foreach (var name in new[] { "Utf8FallbackCandidateVerifier", "Utf8FallbackVerifierRuntimeFactory", "Utf8FallbackVerifierMode" })
            Assert.Null(assembly.GetType("Lokad.Utf8Regex.Internal.Execution." + name));
        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly;
        foreach (var type in assembly.GetTypes())
        {
            foreach (var field in type.GetFields(flags)) Assert.False(RetainsEngine(field.FieldType), $"{type.FullName}.{field.Name}");
            foreach (var method in type.GetMethods(flags)) Assert.False(RetainsEngine(method.ReturnType), $"{type.FullName}.{method.Name}");
            foreach (var constructor in type.GetConstructors(flags))
            foreach (var parameter in constructor.GetParameters())
                Assert.False(RetainsEngine(parameter.ParameterType), $"{type.FullName} {constructor}");
        }

        static bool RetainsEngine(Type type) => type == typeof(Regex) ||
            type.HasElementType && RetainsEngine(type.GetElementType()!) ||
            type.IsGenericType && type.GetGenericArguments().Any(RetainsEngine);
    }

    [Fact]
    public void ProductionRegexConstructionInventoryIsEmpty()
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

        // R05: every shipped profile prepares and executes owned programs.
        Assert.Empty(hits);
        Assert.DoesNotContain(hits, static hit => hit.Contains("Lokad.Utf8Regex.PythonRe", StringComparison.Ordinal));
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
        //    Public execution now uses qualified native paths or the scalar program.
        //    The linear compiled router left this set in R05 once its RightToLeft
        //    predicates read cached fallback options instead of constructing the engine.
        //    Compiled/simple/structural consumers and the general cursor factory
        //    borrow the admitted scalar program, including mixed input and RTL.
        //    Metadata queries borrow the owned capture catalog directly.
        var fallbackFiles = FilesContainingAny("FallbackCandidateVerifier.FallbackRegex", "FallbackRegex.", "fallbackRegex.Count(");
        Assert.Empty(fallbackFiles);

        // 2. BCL enumerator structs are retired across all shipped profiles.
        Assert.Empty(FilesContaining("Regex.ValueSplitEnumerator"));
        Assert.Empty(FilesContaining("Regex.ValueMatchEnumerator"));

        // 3. PythonRe managed execution has been removed; no descriptor may return.
        var managedHits = Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => File.ReadLines(path).Any(line => line.Contains("_managedRegex", StringComparison.Ordinal)))
            .ToArray();
        Assert.Empty(managedHits);

        // 4. Evaluators use the cached scalar program and owned capture histories.
        Assert.Empty(FilesContaining(".NextMatch()"));

        // 5. BCL result plumbing holders. Owner: R02/R03 (owned match/group/capture storage).
        Assert.Equal(new[] { "Utf8MatchContext.cs" }, FilesContaining("Match? _match"));
        Assert.Equal(new[] { "Utf8GroupContext.cs" }, FilesContaining("Group? _group"));
        Assert.Equal(new[] { "Utf8CaptureContext.cs" }, FilesContaining("Capture? _capture"));

        // 6. Regex.Escape is a pure string helper used while building translated patterns.
        Assert.Equal(new[] { "Pcre2ExecutionArchitecture.cs" }, FilesContaining("Regex.Escape("));
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
            new[] { "Utf8PythonRegex.FindAll.cs", "Utf8PythonRegex.Utf8.cs", "Utf8PythonRegex.Value.cs", "Utf8PythonRegex.cs" },
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

    [Fact]
    public void ShippedProjectsDeclareNoExternalEngineDependencies()
    {
        // R05: shipped packages must not gain NuGet engine dependencies. Adapters
        // reference only the core project; the framework supplies the rest.
        var repoRoot = FindRepositoryRoot();
        foreach (var project in new[] { "Lokad.Utf8Regex", "Lokad.Utf8Regex.Pcre2", "Lokad.Utf8Regex.PythonRe" })
        {
            var csproj = File.ReadAllText(Path.Combine(repoRoot, "src", project, project + ".csproj"));
            Assert.DoesNotContain("PackageReference", csproj, StringComparison.Ordinal);
            Assert.DoesNotContain("PCRE.NET", csproj, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ShippedAssembliesReferenceNoExternalEngines()
    {
        // R05: the built packages may reference the framework, shared .NET
        // contracts, and (for adapters) the core library only. Any other
        // reference, native or managed, fails here for explicit review.
        var configuration = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyConfigurationAttribute>(typeof(EngineDependencyInventoryTests).Assembly)?.Configuration ?? "Debug";
        var offenders = new System.Collections.Generic.List<string>();
        foreach (var project in new[] { "Lokad.Utf8Regex", "Lokad.Utf8Regex.Pcre2", "Lokad.Utf8Regex.PythonRe" })
        {
            var dll = Path.Combine(FindRepositoryRoot(), "src", project, "bin", configuration, "net10.0", project + ".dll");
            Assert.True(File.Exists(dll), "Missing built assembly: " + dll);
            var references = System.Reflection.Assembly.LoadFrom(dll).GetReferencedAssemblies();
            foreach (var reference in references)
            {
                var name = reference.Name ?? string.Empty;
                var allowed = name.StartsWith("System.", StringComparison.Ordinal) ||
                    name.Equals("netstandard", StringComparison.Ordinal) ||
                    name.Equals("mscorlib", StringComparison.Ordinal) ||
                    name.Equals("Lokad.Utf8Regex", StringComparison.Ordinal);
                if (!allowed)
                {
                    offenders.Add(project + " -> " + reference.FullName);
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void VendoredParserForkIsAbsent()
    {
        // Parser flip (2026-09-26): the vendored Runtime/* parser fork was removed by
        // forward deletion under the no-vendor rule. No production source may reference
        // its types, seam, or directory again.
        var repoRoot = FindRepositoryRoot();
        Assert.False(Directory.Exists(Path.Combine(repoRoot, "src", "Lokad.Utf8Regex", "Internal", "FrontEnd", "Runtime")));
        var tokens = new[] { "RuntimeFrontEnd", "FrontEnd.Runtime", "TryParseLegacy", "FromLegacy" };
        var offenders = new System.Collections.Generic.List<string>();
        foreach (var path in Directory.EnumerateFiles(Path.Combine(repoRoot, "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var number = 0;
            foreach (var line in File.ReadLines(path))
            {
                number++;
                foreach (var token in tokens)
                {
                    if (line.Contains(token, StringComparison.Ordinal))
                    {
                        offenders.Add($"{GetRelativePath(path)}:{number}: {line.Trim()}");
                        break;
                    }
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void ProductionSourceReachesBclRegexInternalsOnlyThroughTheOwnedFrontEnd()
    {
        // R05 (parser flip 2026-09-26): BCL regex implementation types (parser, nodes,
        // char classes, writers/compilers/interpreters, persisted assemblies)
        // are reachable only through the owned front-end layer, whose Utf8-prefixed
        // type names share substrings with the BCL vocabulary. Everywhere else they
        // must be qualified through a FrontEnd alias or live in a file with a
        // FrontEnd using. Own IL emission (Utf8Emit*) is the owned engine and is
        // out of scope here. The vendored fork itself is gone; VendoredParserForkIsAbsent
        // pins its absence.
        var repoRoot = FindRepositoryRoot();
        var srcRoot = Path.Combine(repoRoot, "src");
        var tokens = new[]
        {
            "RegexCharClass",
            "RegexParser",
            "RegexNode",
            "RegexWriter",
            "RegexCompiler",
            "RegexInterpreter",
            "RegexPrefix",
            "RegexTree",
            "RegexRunner",
            "RegexCode",
            "RegexBoyerMoore",
            "CompileToAssembly",
            "Assembly.Load",
            "Activator.CreateInstance",
        };
        var offenders = new System.Collections.Generic.List<string>();
        foreach (var path in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                path.Contains($"{Path.DirectorySeparatorChar}Internal{Path.DirectorySeparatorChar}FrontEnd{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(path);
            var hasFrontEndUsing = text.Contains("Internal.FrontEnd", StringComparison.Ordinal);
            var number = 0;
            foreach (var line in File.ReadLines(path))
            {
                number++;
                foreach (var token in tokens)
                {
                    if (!line.Contains(token, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var trimmed = line.Trim();
                    var isComment = trimmed.StartsWith("//", StringComparison.Ordinal);
                    if (line.Contains("FrontEnd", StringComparison.Ordinal) || (hasFrontEndUsing && !isComment))
                    {
                        break;
                    }

                    offenders.Add($"{GetRelativePath(path)}:{number}: {trimmed}");
                    break;
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void EmittedCodeReachesNoBclRegexEngine()
    {
        // R05 IL/generated-code check: the DynamicMethod emitters (Utf8Emit*.cs)
        // may only call owned helpers, owned search state, and ReadOnlySpan<byte>
        // primitives. No emitted site may construct, reference, or run the BCL
        // regex engine; audited call targets are the emitters own private methods
        // plus span primitives. Own Utf8 names and plan types are out of scope here.
        var repoRoot = FindRepositoryRoot();
        var tokens = new[]
        {
            "new Regex(",
            "typeof(Regex",
            "RegexRunner",
            "RegexCompiler",
            "RegexInterpreter",
            "RegexWriter",
            "CompileToAssembly",
        };
        var offenders = new System.Collections.Generic.List<string>();
        foreach (var path in Directory.EnumerateFiles(Path.Combine(repoRoot, "src", "Lokad.Utf8Regex"), "Utf8Emit*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var number = 0;
            foreach (var line in File.ReadLines(path))
            {
                number++;
                foreach (var token in tokens)
                {
                    if (line.Contains(token, StringComparison.Ordinal))
                    {
                        offenders.Add($"{GetRelativePath(path)}:{number}: {line.Trim()}");
                        break;
                    }
                }
            }
        }

        Assert.Empty(offenders);
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

