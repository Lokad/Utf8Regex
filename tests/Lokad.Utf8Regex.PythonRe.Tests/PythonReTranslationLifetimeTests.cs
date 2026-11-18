using System.Reflection;
using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReTranslationLifetimeTests
{
    [Theory]
    [InlineData("([a-z]+)?", 4)]
    [InlineData("(?:ab|cd)?", 4)]
    [InlineData("(a)?(b)?", 4)]
    [InlineData("(?s:.*)", 2)]
    public void NullableNonliteralsUseScalarProgressionWithoutUnusedCorePreparation(string pattern, int count)
    {
        {
            using var regex = new Utf8PythonRegex(pattern);
            var input = "ab😀!"u8.ToArray();
            Assert.False(regex.DebugUsesUtf8RegexBackend);
            Assert.True(regex.IsMatch(input));
            Assert.True(regex.Match(input).Success);
            Assert.Equal(count, regex.Count(input));
            var collectedRanges1 = new List<Utf8PythonRangeMatch>();
            using (var rangeCursor1 = regex.CreateMatchCursor(input, 0, default))
            {
                while (rangeCursor1.MoveNext(out var rangeMatch1))
                    collectedRanges1.Add(rangeMatch1);
            }
            var matches = collectedRanges1.ToArray();
            Assert.Equal(count, matches.Length);
            Assert.Equal(count == 2 ? new[] { 0, 7 } : new[] { 0, 2, 6, 7 },
                matches.Select(match => match.Value.StartOffsetInBytes));
            Assert.Equal(7, matches[^1].Value.EndOffsetInBytes);
            Assert.Equal(count == 2 ? "XX" : "XX😀X!X", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "X")));
            Assert.Equal(count == 2, regex.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
                regex.Count(input, 0, cancellation.Token)).CancellationToken);
        }
    }
    [Fact]
    public void ExactEmptyLiteralRetainsItsLiveCoreValueRoute()
    {
        using var regex = new Utf8PythonRegex("");
        Assert.True(regex.DebugUsesUtf8RegexBackend);
        Assert.True(regex.DebugUsesZeroOffsetUtf8SearchValueFastPath);
        Assert.Equal(3, regex.Count("😀!"u8));
        Assert.Equal(0, regex.Match("😀!"u8, mode: Utf8PythonMatchMode.Search).EndOffsetInBytes);
        Assert.True(regex.Match(ReadOnlySpan<byte>.Empty, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(regex.Match("😀!"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
    }
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
            Assert.True(regex.Match(input, mode: Utf8PythonMatchMode.Search).Success);
            Assert.True(regex.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success);
            Utf8PythonRangeMatch snapshot1 = default;
            using (var captureCursor1 = regex.CreateMatchCursor(input, mode: Utf8PythonMatchMode.Search))
            {
                captureCursor1.MoveNext(out snapshot1);
            }
            Assert.True(snapshot1.Success);
            ReadOnlyMemory<byte> snapshots1Input = input;
            var snapshots1 = new List<Utf8PythonRangeMatch>();
            using (var snapshots1Cursor = regex.CreateMatchCursor(snapshots1Input, 0, default))
            {
                while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                    snapshots1.Add(snapshots1Match);
            }
            Assert.Equal(1, snapshots1.Count);
            Assert.Equal("X", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "X")));
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
        Assert.True(regex.Match(input, mode: Utf8PythonMatchMode.Search).Success);
        Assert.True(regex.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success);
        Utf8PythonRangeMatch snapshot2 = default;
        using (var captureCursor2 = regex.CreateMatchCursor(input, mode: Utf8PythonMatchMode.Search))
        {
            captureCursor2.MoveNext(out snapshot2);
        }
        Assert.True(snapshot2.Success);
        ReadOnlyMemory<byte> snapshots2Input = input;
        var snapshots2 = new List<Utf8PythonRangeMatch>();
        using (var snapshots2Cursor = regex.CreateMatchCursor(snapshots2Input, 0, default))
        {
            while (snapshots2Cursor.MoveNext(out var snapshots2Match))
                snapshots2.Add(snapshots2Match);
        }
        Assert.Equal(1, snapshots2.Count);
        Assert.Equal("X", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "X")));
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
        Assert.True(regex.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.Equal(1, regex.Count(input));
        Utf8PythonRangeMatch snapshot3 = default;
        using (var captureCursor3 = regex.CreateMatchCursor(input, mode: Utf8PythonMatchMode.Search))
        {
            captureCursor3.MoveNext(out snapshot3);
        }
        Assert.True(snapshot3.Success);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProvedLiteralLookaheadRetainsCompleteNativeResultEligibility(bool finite)
    {
        var policy = finite
            ? new Utf8ResourcePolicy
            {
                MaxWorkSteps = 100_000,
                MaxDepth = 1024,
                MaxWorkingBytes = 1_048_576,
                MaxResultBytes = 1_048_576
            }
            : default;
        var regex = new Utf8PythonRegex("needle(?=bar)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        });
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        var core = Assert.IsType<Utf8Regex>(typeof(Utf8PythonRegex).GetField("_utf8Regex", fields)!.GetValue(regex));
        var program = Assert.IsType<Lokad.Utf8Regex.Internal.Execution.Utf8SliceProgram>(
            typeof(Utf8PythonRegex).GetField("_scalarProgram", fields)!.GetValue(regex));
        Assert.True(core.CanFindNativeLiteralForResult);
        Assert.True(PythonReScalarMatchState.CanUseCandidateRegex(program, core));
        PythonReMatchAssert.Equal("needle", regex.Match("xxneedlebar"u8, mode: Utf8PythonMatchMode.Search));
        Assert.Equal(1, regex.Count("xxneedlebar"u8));
        ReadOnlyMemory<byte> snapshots3Input = ("xxneedlebar"u8).ToArray();
        var snapshots3 = new List<Utf8PythonRangeMatch>();
        using (var snapshots3Cursor = regex.CreateMatchCursor(snapshots3Input, 0, default))
        {
            while (snapshots3Cursor.MoveNext(out var snapshots3Match))
                snapshots3.Add(snapshots3Match);
        }
        Assert.Equal(["needle"], snapshots3.Select(captureSnapshot1 => System.Text.Encoding.UTF8.GetString(captureSnapshot1.GetGroupMemory(captureSnapshot1.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        Assert.False(regex.IsMatch("needlex"u8));
        Assert.False(regex.Match("xxneedlexbar"u8, mode: Utf8PythonMatchMode.Search).Success);
        Assert.False(regex.Match("needlebar"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
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
            var copiedNames40 = new string[regex.CaptureGroupCount + 1];
            for (var captureSlot40 = 0; captureSlot40 < copiedNames40.Length; captureSlot40++)
            {
                Assert.Equal(System.Buffers.OperationStatus.DestinationTooSmall,
                    regex.TryCopyGroupNameUtf8(captureSlot40, [], out var nameLength40));
                var nameBytes40 = new byte[nameLength40];
                Assert.Equal(System.Buffers.OperationStatus.Done,
                    regex.TryCopyGroupNameUtf8(captureSlot40, nameBytes40, out var written40));
                Assert.Equal(nameLength40, written40);
                copiedNames40[captureSlot40] = System.Text.Encoding.UTF8.GetString(nameBytes40);
            }
            Assert.Equal(["0", "α"], copiedNames40);
            Utf8PythonRangeMatch hit = default;
            using (var captureCursor4 = regex.CreateMatchCursor(("ab"u8).ToArray(), mode: Utf8PythonMatchMode.Search))
            {
                captureCursor4.MoveNext(out hit);
            }
            Assert.True(hit.Success);
            Assert.Equal("a", System.Text.Encoding.UTF8.GetString((hit.Groups[1].Success ? hit.GetGroupMemory(hit.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
            Assert.Equal("α", hit.LastGroup);
            Utf8PythonRangeMatch snapshot5 = default;
            using (var captureCursor5 = regex.CreateMatchCursor(("ab"u8).ToArray(), mode: Utf8PythonMatchMode.FullMatch))
            {
                captureCursor5.MoveNext(out snapshot5);
            }
            Assert.True(snapshot5.Success);
            Assert.Equal("a", System.Text.Encoding.UTF8.GetString(regex.Replace("ab"u8, @"\g<α>", 1)));
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
            if (!seen.Add(value))
            {
                continue;
            }
            if (value is string text)
            {
                yield return text;
                continue;
            }
            if (value is Array array)
            {
                if (array.GetType().GetElementType()?.IsPrimitive == true)
                {
                    continue;
                }
                foreach (var item in array)
                {
                    if (item is not null)
                    {
                        pending.Push(item);
                    }
                }
                continue;
            }
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum ||
                type.Assembly != typeof(Utf8PythonRegex).Assembly && type.Assembly != typeof(Utf8Regex).Assembly)
            {
                continue;
            }
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.GetValue(value) is { } item)
                {
                    pending.Push(item);
                }
            }
        }
    }
}
