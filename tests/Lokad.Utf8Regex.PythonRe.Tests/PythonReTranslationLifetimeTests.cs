using System.Reflection;
using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReTranslationLifetimeTests
{
    [Theory]
    [InlineData("distinct_native_literal_abc123")]
    [InlineData("(?P<nomé>é😀)+")]
    [InlineData("(?#owned source only)alpha|omega")]
    public void NativeCompilationAndExplicitPatternProjectionDoNotRetainWholePatternText(string pattern)
    {
        using var regex = new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern));
        Assert.Equal(pattern, regex.Pattern);
        Assert.DoesNotContain(ReachableStrings(regex), text => text == pattern);
    }

    [Theory]
    [InlineData(@"\w+", "alpha")]
    [InlineData(@"(?P<word>\w+)", "alpha")]
    [InlineData(@"\d+", "123")]
    [InlineData(@"\s+", " \t")]
    [InlineData(@"prefix\w+", "prefixalpha")]
    public void DeclinedCorePreparationIsNotRetained(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var regex in new[] { new Utf8PythonRegex(pattern), new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern)) })
        {
            Assert.False(regex.DebugUsesUtf8RegexBackend);
            Assert.True(regex.IsMatch(input));
            Assert.Equal(1, regex.Count(input));
            Assert.True(regex.Search(input).Success);
            Assert.True(regex.FullMatch(input).Success);
            Assert.True(regex.SearchDetailedData(input).Success);
            Assert.Equal(1, regex.FindAllToStrings(input).Count);
            Assert.Equal("X", regex.ReplaceToString(input, "X"));
        }
    }

    [Theory]
    [InlineData(@"([0-9]+)", "123")]
    [InlineData(@"(?P<n>[0-9]+)", "123")]
    [InlineData(@"([a-z]+)", "alpha")]
    [InlineData(@"(?P<word>[a-z]+)", "alpha")]
    [InlineData(@"(ab[0-9]{2})", "ab12")]
    [InlineData(@"[0-9]+", "123")]
    [InlineData(@"foo(?=bar)bar", "foobar")]
    public void ReviewedEligibilityShapesKeepTheirPublicResults(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8PythonRegex(pattern);
        Assert.True(regex.IsMatch(input));
        Assert.Equal(1, regex.Count(input));
        Assert.True(regex.Search(input).Success);
        Assert.True(regex.FullMatch(input).Success);
        Assert.True(regex.SearchDetailedData(input).Success);
        Assert.Equal(1, regex.FindAllToStrings(input).Count);
        Assert.Equal("X", regex.ReplaceToString(input, "X"));
    }

    [Theory]
    [InlineData("needle", "needle")]
    [InlineData("😀", "😀")]
    [InlineData("foo|bar", "foo")]
    [InlineData(@"(?P<word>needle)", "needle")]
    public void LiveNativeResultPreparationsRemain(string pattern, string text)
    {
        var regex = new Utf8PythonRegex(pattern);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.True(regex.DebugUsesUtf8RegexBackend);
        Assert.True(regex.FullMatch(input).Success);
        Assert.Equal(1, regex.Count(input));
        Assert.True(regex.SearchDetailedData(input).Success);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealLookaheadCandidateRemainsWithoutClaimingLiteralResultEligibility(bool finite)
    {
        var policy = finite
            ? new Utf8ResourcePolicy { MaxWorkSteps = 100_000, MaxDepth = 1024,
                MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576 }
            : default;
        var regex = new Utf8PythonRegex("needle(?=bar)", PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, policy);
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        var core = Assert.IsType<Utf8Regex>(typeof(Utf8PythonRegex).GetField("_utf8Regex", fields)!.GetValue(regex));
        var program = Assert.IsType<Lokad.Utf8Regex.Internal.Execution.Utf8SliceProgram>(
            typeof(Utf8PythonRegex).GetField("_scalarProgram", fields)!.GetValue(regex));
        Assert.False(core.CanFindNativeLiteralForResult);
        Assert.True(PythonReScalarMatchState.CanUseCandidateRegex(program, core));
        Assert.Equal("needle", regex.SearchToString("xxneedlebar"u8));
        Assert.Equal(1, regex.Count("xxneedlebar"u8));
        Assert.Equal(["needle"], regex.FindAllToStrings("xxneedlebar"u8).ScalarValues);
        Assert.False(regex.IsMatch("needlex"u8));
        Assert.False(regex.FullMatch("needlebar"u8).Success);
    }

    [Fact]
    public void ScalarOnlyInstancesDoNotRetainUnusedRenderedPatterns()
    {
        const string pattern = "(?P<α>a)?(?(α)b|)";
        var parse = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
        var rendered = Encoding.UTF8.GetString(PythonReTranslator.Translate(parse, true).NativePatternUtf8);
        Assert.NotEqual(pattern, rendered);
        foreach (var regex in new[] { new Utf8PythonRegex(pattern), new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern)) })
        {
            Assert.False(regex.DebugUsesUtf8RegexBackend);
            Assert.DoesNotContain(ReachableStrings(regex), text => text == rendered);
            Assert.Equal(["0", "α"], regex.GetGroupNames());
            var hit = regex.SearchDetailedData("ab"u8);
            Assert.True(hit.Success);
            Assert.Equal("a", hit.Groups[1].ValueText);
            Assert.Equal("α", hit.LastGroup);
            Assert.True(regex.FullMatchDetailedData("ab"u8).Success);
            Assert.Equal("a", regex.ReplaceToString("ab"u8, @"\g<α>", 1));
        }
    }

    [Theory]
    [InlineData("(?(missing)a|)")]
    [InlineData("(?:(?<=a+)b)?(a)?(?(1)b|)")]
    public void SkippingNativeRenderingStillValidatesReferencesAndLookbehind(string pattern)
    {
        Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(pattern));
        Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern)));
    }

    [Fact]
    public void ScalarOnlyTranslationAdmitsLessWorkAndStorageWithoutChangingNames()
    {
        const string pattern = "(?P<α>a)?(?(α)b|)";
        var rendered = Translate(true);
        var scalar = Translate(false);
        Assert.Empty(scalar.Pattern);
        Assert.Equal(rendered.Name, scalar.Name);
        Assert.Equal("α", scalar.Name);
        Assert.True(scalar.Work < rendered.Work);
        Assert.True(scalar.PeakBytes < rendered.PeakBytes);

        static (byte[] Pattern, string Name, ulong Work, ulong PeakBytes) Translate(bool render)
        {
            using var scope = Utf8ConstructionScope.Enter();
            var parse = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
            var translation = PythonReTranslator.Translate(parse, render);
            var name = PythonReScalarCompiler.Compile(parse).GroupNames[1];
            return (translation.NativePatternUtf8, name, scope.Budget.WorkSteps, scope.Budget.PeakBytes);
        }
    }

    private static IEnumerable<string> ReachableStrings(object root)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<object>();
        pending.Push(root);
        while (pending.TryPop(out var value))
        {
            if (!seen.Add(value)) continue;
            if (value is string text) { yield return text; continue; }
            if (value is Array array)
            {
                if (array.GetType().GetElementType()?.IsPrimitive == true) continue;
                foreach (var item in array) if (item is not null) pending.Push(item);
                continue;
            }
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum ||
                type.Assembly != typeof(Utf8PythonRegex).Assembly && type.Assembly != typeof(Utf8Regex).Assembly) continue;
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (field.GetValue(value) is { } item) pending.Push(item);
        }
    }
}
