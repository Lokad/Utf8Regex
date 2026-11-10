using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Tests.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// Production code must remain independent of BCL matcher/result execution and
/// BCL pattern escaping. Compatibility option/timeout types and constants retain
/// their explicit metadata role; they do not execute a subject.
/// </summary>
public sealed class EngineDependencyInventoryTests
{
    [Fact]
    public void ConsumedCoreHasNoBclRegexFactoriesOrEngineOwners()
    {
        var assembly = typeof(Utf8Regex).Assembly;
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

        // Representation names, decoded-storage layouts and the concrete
        // instruction payload shape are implementation choices. The consumed
        // PE/member policy below enforces independence from BCL matching.

        static bool RetainsEngine(Type type) => type == typeof(Regex) || type == typeof(Match) || type == typeof(Group) || type == typeof(Capture) ||
            type.HasElementType && RetainsEngine(type.GetElementType()!) ||
            type.IsGenericType && type.GetGenericArguments().Any(RetainsEngine);
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
            (RegexOptions)0x1000,
            RegexOptions.ECMAScript | RegexOptions.Singleline,
            RegexOptions.NonBacktracking | RegexOptions.RightToLeft,
        })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Regex("foo", options));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Regex("foo", options));
        }
        // .NET 11 assigns 0x0800 to AnyNewLine. Runtime support does not
        // silently extend the owned grammar or its admitted option set.
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Regex("foo", (RegexOptions)0x0800));
    }


    [Fact]
    [Trait("Category", "SourceOnly")]
    public void CorePythonReIntegrationPointsAreMarked()
    {
        // Keep the removable profile boundary, without pinning filenames of
        // legitimate adapter consumers. PCRE2 has its own isolation guard.
        var srcRoot = Path.Combine(FindRepositoryRoot(), "src");
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
    public void ConsumedCoreReferencesNoExternalMatchers()
    {
        // Inspect what this fixture consumes, also in detached package runs.
        // Each adapter's own fixture owns that adapter's inventory.
        AssemblyDependencyInventory.AssertNoExternalMatchers(typeof(Utf8Regex).Assembly.Location);
    }

    [Theory]
    [InlineData("System.Text.RegularExpressions.Regex.IsMatch(\"a\", \"a\")", false)]
    [InlineData("System.Text.RegularExpressions.Regex.Escape(\"a\")", false)]
    [InlineData("new System.Text.RegularExpressions.Regex(\"a\")", false)]
    [InlineData("typeof(System.Text.RegularExpressions.Match)", false)]
    [InlineData("System.Text.RegularExpressions.Regex.InfiniteMatchTimeout", true)]
    [InlineData("System.Text.RegularExpressions.RegexOptions.IgnoreCase", true)]
    [InlineData("new System.Text.RegularExpressions.RegexMatchTimeoutException()", true)]
    public void TypedMemberPolicyDistinguishesMatchingFromCompatibility(string expression, bool allowed)
    {
        // Compile the fully qualified static-call witness which the former
        // source-spelling/owner checks missed. Nothing from this PE is executed.
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("MemberPolicyFixture",
            [CSharpSyntaxTree.ParseText("internal static class Fixture { internal static object Probe() => " + expression + "; }")],
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
        stream.Position = 0;
        using var pe = new PEReader(stream);
        Assert.Equal(allowed, AssemblyDependencyInventory.FindBclMatcherReferences(pe.GetMetadataReader()).Length == 0);
    }

    [Fact]
    [Trait("Category", "SourceOnly")]
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

