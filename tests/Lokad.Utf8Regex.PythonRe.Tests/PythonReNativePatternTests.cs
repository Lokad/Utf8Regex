using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReNativePatternTests
{
    public static IEnumerable<object[]> GrammarCases()
    {
        var seen = new HashSet<(string, string)>();
        foreach (var row in PythonReScalarGlobalTests.GlobalCases())
            if (seen.Add(((string)row[0], (string)row[1])))
                yield return [row[0], row[1], PythonReCompileOptions.None];
        yield return ["(?P<nomé>😀+)(?P<empty>)(?P<absent>x)?", "a😀😀b", PythonReCompileOptions.None];
        yield return ["(?<=é)(?P<nomé>😀+)\\1", "é😀😀é😀😀😀😀", PythonReCompileOptions.None];
        yield return ["[é-ê😀-🙏]+", "xé😀🙏ê", PythonReCompileOptions.None];
        yield return ["\\é+|\\😀+", "éé😀😀", PythonReCompileOptions.None];
        yield return [@"\x61\u00e9\U0001f600+", "aé😀😀", PythonReCompileOptions.None];
        yield return ["a\0e\u0301😀+", "a\0e\u0301😀😀", PythonReCompileOptions.None];
        yield return ["(?i:é|K|k|𐐀)+", "ÉKk𐐨", PythonReCompileOptions.None];
        yield return ["(?x) (?# 😀é ) (?P<é>😀+) # note\\\ncontinued\n", "a😀😀b", PythonReCompileOptions.None];
        yield return ["a++a|(?>é+)b", "aaaaéébééx", PythonReCompileOptions.None];
        yield return ["(?i:é|k)+", "ÉKkK", PythonReCompileOptions.Ascii];
    }

    [Theory]
    [MemberData(nameof(GrammarCases))]
    public void PublicNativeGrammarPreservesScalarResultsAfterSourceReuse(string pattern, string subject, PythonReCompileOptions options)
    {
        var bytes = Encoding.UTF8.GetBytes(pattern);
        using var native = new Utf8PythonRegex(bytes, options);
        using var text = new Utf8PythonRegex(pattern, options);
        Array.Fill(bytes, (byte)0xff);
        Assert.Equal(pattern, native.Pattern);
        Assert.Equal(text.EffectiveOptions, native.EffectiveOptions);
        Assert.Equal(text.GetGroupNames(), native.GetGroupNames());
        var input = Encoding.UTF8.GetBytes(subject);
        Assert.Equal(text.Count(input), native.Count(input));
        Assert.Equal(text.IsMatch(input), native.IsMatch(input));
        var expected = text.FindIterDetailed(input);
        var actual = native.FindIterDetailed(input);
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].Groups.Length, actual[index].Groups.Length);
            for (var group = 0; group < expected[index].Groups.Length; group++)
            {
                var a = expected[index].Groups[group]; var b = actual[index].Groups[group];
                Assert.Equal(a, b);
            }
        }
    }

    [Theory]
    [InlineData("é😀)")]
    [InlineData("😀(?P<x>a)(?P=x)(?P=missing)")]
    [InlineData("é[😀-é]")]
    [InlineData("😀\\u12")]
    [InlineData("é\\UFFFFFFFF")]
    [InlineData("😀(?<é)")]
    [InlineData("é😀(?Pé)")]
    [InlineData("😀(?P<😀>a)")]
    [InlineData("é😀(?-u:a)")]
    [InlineData("😀(?#unterminated")]
    public void NativeSyntaxRetainsPublishedUtf16PositionsAndErrorKinds(string pattern)
    {
        var text = Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(pattern));
        var native = Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern)));
        Assert.Equal((text.Position, text.ErrorSource, text.ErrorKind, text.Message),
            (native.Position, native.ErrorSource, native.ErrorKind, native.Message));
    }

    [Fact]
    public void LargeNativeCommentKeepsOnlyOwnedSourceBytesAndSelectedMetadata()
    {
        var pattern = "(?#" + string.Concat(Enumerable.Repeat("😀é", 8192)) + ")(?P<nomé>x)";
        var bytes = Encoding.UTF8.GetBytes(pattern);
        static ulong Held(object source)
        {
            using var construction = Utf8ConstructionScope.Enter();
            using var regex = source is string text ? new Utf8PythonRegex(text) : new Utf8PythonRegex((byte[])source);
            Assert.Equal(["0", "nomé"], regex.GetGroupNames());
            return construction.Budget.ReservedBytes;
        }
        var borrowed = Held(pattern);
        var owned = Held(bytes);
        Assert.Equal(borrowed + Utf8ConstructionBudget.ArrayBytes<byte>(bytes.Length), owned);
    }

    [Fact]
    public void NativeWarmCallsAndCursorCreationDoNotProjectWholePatternText()
    {
        var pattern = "(?#" + string.Concat(Enumerable.Repeat("😀é", 16384)) + ")(?P<word>é)+";
        using var regex = new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern));
        var input = "éé"u8.ToArray();
        for (var i = 0; i < 16; i++)
        {
            _ = regex.Count(input);
            using var cursor = regex.CreateMatchCursor(input);
            _ = cursor.MoveNext(out _);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        var count = 0;
        for (var i = 0; i < 8; i++)
        {
            count += regex.Count(input);
            using var cursor = regex.CreateMatchCursor(input);
            if (cursor.MoveNext(out _)) count++;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(16, count);
        // Sixteen public operations allocate less than one complete source-text
        // copy. Their small VM/result metadata remains intentionally allowed.
        Assert.True(allocated < pattern.Length * 2L, $"Allocated {allocated} bytes for native operations.");
    }

    [Theory]
    [InlineData("(?x:a\\ b)", "a b", "ab")]
    [InlineData("(?x:a\\ b)|c", "a b", "ab")]
    [InlineData("(?x:a(?-x: )b)", "a b", "ab")]
    public void ScopedVerboseLiteralSpacesKeepTheirMeaning(string pattern, string hit, string miss)
    {
        foreach (var regex in new[] { new Utf8PythonRegex(pattern), new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern)) })
        {
            using (regex)
            {
                Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(hit)));
                Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes(miss)));
            }
        }
    }

    [Fact]
    public void FailedNativeDiagnosticProjectionCanBeInterruptedWithoutPublishingAProgram()
    {
        var pattern = Encoding.UTF8.GetBytes(new string('é', 512) + ")");
        ulong totalWork;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            Assert.Equal(512, Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(pattern)).Position);
            totalWork = measured.Budget.WorkSteps;
        }
        using var token = new CancellationTokenSource();
        using var scope = Utf8ConstructionScope.Enter();
        scope.Budget.CancellationToken = token.Token;
        // The final prefix projection visits 512 scalars after syntax fails.
        // Cancel halfway through that work, rather than during validation/parse.
        scope.Budget.CheckpointHook = work => { if (work >= totalWork - 256) token.Cancel(); };
        var failure = Assert.Throws<OperationCanceledException>(() => new Utf8PythonRegex(pattern));
        Assert.Equal(token.Token, failure.CancellationToken);
        Assert.Equal(0UL, scope.Budget.ReservedBytes);
    }
}
