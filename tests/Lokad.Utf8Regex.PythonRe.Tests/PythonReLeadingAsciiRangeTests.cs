using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReLeadingAsciiRangeTests
{
    private const string Pattern = "(?P<word>[a-z]+)";
    private static readonly Utf8ExecutionLimits Limits = new(ulong.MaxValue, 32, 1 << 20, 1 << 20);

    private static Utf8SliceProgram Compile(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern);
        return (Utf8SliceProgram)typeof(Utf8PythonRegex).GetField("_scalarProgram",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(regex)!;
    }

    [Theory]
    [InlineData("[a-z]+", 97, 122)]
    [InlineData("(?P<word>[a-z]+)", 97, 122)]
    [InlineData("([a-z]{2,4})!", 97, 122)]
    [InlineData(@"[\x00-\x7f]+", 0, 127)]
    [InlineData(@"[\x00-\x3f]+", 0, 63)]
    [InlineData(@"[\x40-\x7f]+", 64, 127)]
    [InlineData(@"[\x3f-\x40]+", 63, 64)]
    [InlineData(@"[\x7f]+", 127, 127)]
    [InlineData("[a-cd-f]+", 97, 102)]
    [InlineData("[A-Za-z_][A-Za-z0-9_]*", 65, 122)]
    [InlineData("(?P<word>[a-cx-z]+)", 97, 122)]
    [InlineData("[a-cx-z]+", 97, 122)]
    [InlineData("[ACE]+", 65, 69)]
    public void MandatoryMaskIsContainedInTheDerivedAsciiInterval(string pattern, int minimum, int maximum)
    {
        var program = Compile(pattern);
        Assert.True(PythonReScalarMatchState.TryGetLeadingAsciiRange(program, out var low, out var high, out var widerStart));
        Assert.False(widerStart);
        Assert.Equal(minimum, low);
        Assert.Equal(maximum, high);
        var mask = program.SetMasks[program.Instructions.First(instruction => instruction.Kind == Utf8SliceInstructionKind.Set).A];
        for (var scalar = 0; scalar < 128; scalar++)
        {
            if ((mask[scalar >> 3] & (1 << (scalar & 7))) != 0)
            {
                Assert.InRange(scalar, low, high);
            }
        }
        Assert.NotEqual(0, mask[low >> 3] & (1 << (low & 7)));
        Assert.NotEqual(0, mask[high >> 3] & (1 << (high & 7)));
    }

    [Theory]
    [InlineData("[^a-z]+")]
    [InlineData("[a-z]+?Z")]
    [InlineData("[é-ö]+")]
    [InlineData("(?:a|😀)")]
    [InlineData("(?:a|[^b])")]
    [InlineData("(a?)*Z")]
    [InlineData("a|")]
    [InlineData("(?>a??)b")]
    public void UnprovedPrefixesKeepTheExistingCandidateLoop(string pattern)
    {
        Assert.False(PythonReScalarMatchState.TryGetLeadingAsciiRange(Compile(pattern), out _, out _, out _));
    }

    [Theory]
    [InlineData("(,)?;", 44, 59)]
    [InlineData("(,?);", 44, 59)]
    [InlineData("[a-z]*Z", 90, 122)]
    [InlineData("([a-z]+)?Z", 90, 122)]
    [InlineData("(?:([a-c]+)|([x-z]+))!", 97, 122)]
    [InlineData("(?=[a-z])(?:a|z)", 97, 122)]
    [InlineData("(?>a?)b", 97, 98)]
    [InlineData("(?>ab+)c", 97, 97)]
    [InlineData("a++b", 97, 97)]
    [InlineData("(?>(ab+))c", 97, 97)]
    [InlineData("(?>(?:ab+|de+))c", 97, 100)]
    [InlineData("(?>[a-z]+)!", 97, 122)]
    [InlineData("(?i:(?>a+))b", 65, 97)]
    public void DistinctFirstConsumersShareANecessaryAsciiInterval(string pattern, int minimum, int maximum)
    {
        Assert.True(PythonReScalarMatchState.TryGetLeadingAsciiRange(Compile(pattern), out var low, out var high, out var widerStart));
        Assert.False(widerStart);
        Assert.Equal(minimum, low);
        Assert.Equal(maximum, high);
    }

    [Theory]
    [InlineData("(?=[a-z])([a-z]+)")]
    [InlineData("(([a-z]+))")]
    [InlineData(@"\A([a-z]+)")]
    [InlineData("(?:^|(?=x))[a-z]+")]
    [InlineData("(?![0-9])[a-z]+")]
    public void ZeroWidthPrefixesConvergeOnTheSameMandatoryMask(string pattern)
    {
        Assert.True(PythonReScalarMatchState.TryGetLeadingAsciiRange(Compile(pattern), out var low, out var high, out var widerStart));
        Assert.False(widerStart);
        Assert.Equal(97, low);
        Assert.Equal(122, high);
    }

    [Theory]
    [InlineData(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", 48, 57)]
    [InlineData(@"\B\d+", 48, 57)]
    [InlineData(@"(?=[A-Z])\d+", 48, 57)]
    [InlineData(@"(?P<x>\d+)-(?P=x)", 48, 57)]
    [InlineData(@"(?<=:)\w+", 48, 122)]
    [InlineData(@"(?<!:)\s+", 9, 32)]
    [InlineData(@"(?:\d+|[a-z]+)", 48, 122)]
    [InlineData(@"(?P<x>[a-z]+)?\d+", 48, 122)]
    [InlineData(@"[\d\s]+", 9, 57)]
    [InlineData(@"[\d\sQ]+", 9, 81)]
    [InlineData(@"[\w\d\s]+", 9, 122)]
    public void ProfileCategoryProjectionRetainsWiderStarts(string pattern, int minimum, int maximum)
    {
        var program = Compile(pattern);
        Assert.True(PythonReScalarMatchState.TryGetLeadingAsciiRange(program, out var low, out var high, out var widerStart));
        Assert.Equal(minimum, low);
        Assert.Equal(maximum, high);
        Assert.True(widerStart);
    }

    [Theory]
    [InlineData(@"(?i:[a-z]+)")]
    [InlineData(@"[^\d\s]+")]
    [InlineData(@"[\d\S]+")]
    public void UnprovedCategoryDefinitionsDecline(string pattern)
        => Assert.False(PythonReScalarMatchState.TryGetLeadingAsciiRange(Compile(pattern), out _, out _, out _));

    [Theory]
    [InlineData("(?i:sherlock)", 83, 115)]
    [InlineData("(?i:ſ)abc", 83, 115)]
    [InlineData("(?i:K)abc", 75, 107)]
    [InlineData("(?i:İ)abc", 73, 105)]
    [InlineData("(?i:ı)abc", 73, 105)]
    [InlineData("[éa]+", 97, 97)]
    public void SingleUnicodeRemainderLeavesTheAsciiProjectionUnchanged(string pattern, int minimum, int maximum)
    {
        var program = Compile(pattern);
        Assert.True(PythonReScalarMatchState.TryGetLeadingAsciiRange(program, out var low, out var high, out var widerStart));
        Assert.Equal(minimum, low);
        Assert.Equal(maximum, high);
        Assert.True(widerStart);
    }

    [Fact]
    public void CategoryUnionSkipsAnAsciiPrefixAndKeepsWideCaptureOffsets()
    {
        using var regex = new Utf8PythonRegex(@"(?P<part>[\d\s]+)!", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 160, MaxWorkingBytes = 16 << 10 }
        });
        const int prefixLength = 8192;
        var input = Encoding.UTF8.GetBytes(new string('x', prefixLength) + "😀\u001c١2!");
        using var source = new CancellationTokenSource();
        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out var match, source.Token));
        Assert.Equal(prefixLength + 4, match.Value.StartOffsetInBytes);
        Assert.Equal(prefixLength + 9, match.Value.EndOffsetInBytes);
        Assert.True(match.GetGroupMemory(1).Span.SequenceEqual("\u001c١2"u8));
        Assert.Equal(1, regex.Count(input, cancellationToken: source.Token));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2047)]
    [InlineData(2048)]
    [InlineData(2049)]
    public void FoldedSearchGlobalsKeepCapturesAndScalarOffsets(int padding)
    {
        var regex = new Utf8PythonRegex("(?i:(?P<word>sherlock))");
        var prefix = new string('9', padding);
        var input = Encoding.UTF8.GetBytes(prefix + " SHERLOCK x sherlock!");
        ReadOnlyMemory<byte> snapshots1Input = input;
        var snapshots1 = new List<Utf8PythonRangeMatch>();
        using (var snapshots1Cursor = regex.CreateMatchCursor(snapshots1Input, 0, default))
        {
            while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                snapshots1.Add(snapshots1Match);
        }
        Assert.Equal(new[] { "SHERLOCK", "sherlock" }, snapshots1.Select(captureSnapshot1 => System.Text.Encoding.UTF8.GetString(captureSnapshot1.GetGroupMemory(captureSnapshot1.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        Assert.Equal(padding + 1, regex.Match(input, mode: Utf8PythonMatchMode.Search).StartOffsetInBytes);
        Assert.Equal(padding + 12, regex.Match(input, padding + 2, mode: Utf8PythonMatchMode.Search).StartOffsetInBytes);
        Assert.False(regex.Match(input).Success);
        Assert.False(regex.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.Equal(new[] { prefix + " ", "SHERLOCK", " x ", "sherlock", "!" }, PythonReSplitAssert.Decode(input, regex.SplitRanges(input, 0, 0, default)));
        Assert.Equal(prefix + " [SHERLOCK] x [sherlock]!", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "[\\g<word>]"u8)));
        var collectedRanges1 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor1 = regex.CreateMatchCursor(("π😀ſHERLOCK"u8).ToArray(), 0, default))
        {
            while (rangeCursor1.MoveNext(out var rangeMatch1))
                collectedRanges1.Add(rangeMatch1);
        }

        // A non-ASCII subject keeps all CPython partners on the scalar route.
        var match = collectedRanges1.ToArray()[0];
        Assert.Equal("ſHERLOCK", System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(1).Span));
        Assert.Equal(6, match.Value.StartOffsetInBytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2047)]
    [InlineData(2048)]
    [InlineData(8192)]
    public void UnicodeRemainderSearchGlobalsUseTheAsciiHintUnderFiniteLimits(int padding)
    {
        var regex = new Utf8PythonRegex("(?P<word>[éa]+)", options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 2000,
                MaxDepth = 32,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            }
        });
        var prefix = new string('9', padding);
        var input = Encoding.UTF8.GetBytes(prefix + " aaa x aa!");
        using var token = new CancellationTokenSource();
        ReadOnlyMemory<byte> snapshots2Input = input;
        var snapshots2 = new List<Utf8PythonRangeMatch>();
        using (var snapshots2Cursor = regex.CreateMatchCursor(snapshots2Input, 0, default))
        {
            while (snapshots2Cursor.MoveNext(out var snapshots2Match))
                snapshots2.Add(snapshots2Match);
        }
        Assert.Equal(new[] { "aaa", "aa" }, snapshots2.Select(captureSnapshot2 => System.Text.Encoding.UTF8.GetString(captureSnapshot2.GetGroupMemory(captureSnapshot2.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        Assert.Equal(padding + 1, regex.Match(input, 0, cancellationToken: token.Token, mode: Utf8PythonMatchMode.Search).StartOffsetInBytes);
        Assert.Equal(padding + 2, regex.Match(input, padding + 2, mode: Utf8PythonMatchMode.Search).StartOffsetInBytes);
        Assert.Equal(padding + 7, regex.Match(input, padding + 4, mode: Utf8PythonMatchMode.Search).StartOffsetInBytes);
        Assert.False(regex.Match(input).Success);
        Assert.False(regex.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.Equal(new[] { prefix + " ", "aaa", " x ", "aa", "!" }, PythonReSplitAssert.Decode(input, regex.SplitRanges(input, 0, 0, default)));
        Assert.Equal(prefix + " [aaa] x [aa]!", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "[\\g<word>]"u8)));
        var collectedRanges2 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor2 = regex.CreateMatchCursor(("π😀éa"u8).ToArray(), 0, default))
        {
            while (rangeCursor2.MoveNext(out var rangeMatch2))
                collectedRanges2.Add(rangeMatch2);
        }

        var match = collectedRanges2.ToArray()[0];
        Assert.Equal("éa", System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(1).Span));
        Assert.Equal(6, match.Value.StartOffsetInBytes);
    }

    [Fact]
    public void OptionalProofStopsAtItsFixedWorkAndBranchBounds()
    {
        foreach (var pattern in new[] {
            new string('(', 70) + "[a-z]+" + new string(')', 70),
            string.Concat(Enumerable.Repeat("(?>", 70)) + "[a-z]+" + new string(')', 70),
            new string('(', 17) + "a" + string.Concat(Enumerable.Repeat("|bb)", 17)) })
        {
            Assert.False(PythonReScalarMatchState.TryGetLeadingAsciiRange(Compile(pattern), out _, out _, out _));
            Assert.True(new Utf8PythonRegex(pattern).IsMatch("123 alpha"u8));
        }
    }

    [Theory]
    [InlineData("(,)?;", null)]
    [InlineData("(,?);", "")]
    public void OptionalCapturedSplitKeepsAbsentAndEmptyValuesAcrossBlockTails(string pattern, string? absent)
    {
        var regex = new Utf8PythonRegex(pattern);
        foreach (var padding in new[] { 0, 2047, 2048, 2049, 4097 })
        {
            var prefix = new string('x', padding) + "😀";
            var input = Encoding.UTF8.GetBytes(prefix + "alpha;beta,;gamma;");
            Assert.Equal(new[] { prefix + "alpha", absent, "beta", ",", "gamma", absent, "" }, PythonReSplitAssert.Decode(input, regex.SplitRanges(input, 0, 0, default)));
            Assert.Equal(new[] { "alpha", absent, "beta", ",", "gamma", absent, "" },
                PythonReSplitAssert.Decode(input, regex.SplitRanges(input, 0, Encoding.UTF8.GetByteCount(prefix), default)));
            Assert.Equal(new[] { "alpha", absent, "beta,;gamma;" },
                PythonReSplitAssert.Decode(input, regex.SplitRanges(input, 1, Encoding.UTF8.GetByteCount(prefix), default)));
        }
    }

    [Fact]
    public void OptionalCaptureAliasesKeepMissingGroupsAndRetainedValues()
    {
        Utf8PythonRangeMatch cursorRange = default;
        using var cursor = new Utf8PythonRegex("(?P<sep>,)?;").CreateMatchCursor("x😀,;y;z;"u8.ToArray());
        var alias = cursor;
        Assert.True(cursor.MoveNext(out cursorRange));
        var first = cursorRange;
        Assert.Equal(",", System.Text.Encoding.UTF8.GetString(first.GetGroupMemory(1).Span));
        Assert.Equal(5, first.Value.StartOffsetInBytes);
        Assert.True(alias.MoveNext(out cursorRange));
        Assert.False(cursorRange.Groups[1].Success);
        while (cursor.MoveNext(out cursorRange))
        {
        }
        alias.Dispose();
        Assert.Equal(",;", System.Text.Encoding.UTF8.GetString(first.GetValueMemory().Span));
        Assert.Equal(",", System.Text.Encoding.UTF8.GetString(first.GetGroupMemory(1).Span));
    }

    [Theory]
    [InlineData("(,)?;")]
    [InlineData("[a-z]*Z")]
    [InlineData("([a-z]+)?Z")]
    [InlineData("(a|z)!")]
    [InlineData("(?:([a-c]+)|([x-z]+))!")]
    [InlineData("(?=[a-z])(?:a|z)")]
    [InlineData("(?>ab+)c")]
    [InlineData("(?>([a-c]+))!")]
    [InlineData("(?>a|ab)b")]
    [InlineData("(?>ab|a)b")]
    [InlineData("(?>(?:a|z))!")]
    public void BranchIntervalsPreserveEveryMatchAndCaptureAcrossMixedScalarStarts(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern);
        var oracle = new Regex(pattern);
        string[] pieces = ["", ",", ";", "a", "Z", "z", "é", "😀"];
        foreach (var first in pieces)
        {
            foreach (var second in pieces)
            {
                var text = "x" + first + second + "abc!;Zza!;";
                var input = Encoding.UTF8.GetBytes(text);
                foreach (var start in new[] { 0, 1 + first.Length })
                {
                    var expected = oracle.Matches(text, start).Cast<Match>().ToArray();
                    var collectedRanges3 = new List<Utf8PythonRangeMatch>();
                    using (var rangeCursor3 = regex.CreateMatchCursor(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, start)), default))
                    {
                        while (rangeCursor3.MoveNext(out var rangeMatch3))
                            collectedRanges3.Add(rangeMatch3);
                    }
                    var actual = collectedRanges3.ToArray();
                    Assert.Equal(expected.Length, actual.Length);
                    for (var index = 0; index < expected.Length; index++)
                    {
                        Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected[index].Index)), actual[index].Value.StartOffsetInBytes);
                        Assert.Equal(expected[index].Groups.Count, actual[index].Groups.Length);
                        for (var group = 0; group < expected[index].Groups.Count; group++)
                        {
                            Assert.Equal(expected[index].Groups[group].Success, actual[index].Groups[group].Success);
                            if (expected[index].Groups[group].Success)
                            {
                                Assert.Equal(expected[index].Groups[group].Value, System.Text.Encoding.UTF8.GetString(actual[index].GetGroupMemory(group).Span));
                            }
                        }
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2047)]
    [InlineData(2048)]
    [InlineData(2049)]
    [InlineData(4097)]
    public void CapturesAndCoordinatesSurviveSearchBlockTailsAndStartOffsets(int padding)
    {
        var prefix = new string('9', padding) + "😀";
        var text = prefix + "alpha𐐀beta!gamma";
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8PythonRegex(Pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 100000,
                MaxDepth = 32,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            }
        });
        using var token = new CancellationTokenSource();
        ReadOnlyMemory<byte> snapshots3Input = input;
        var snapshots3 = new List<Utf8PythonRangeMatch>();
        using (var snapshots3Cursor = regex.CreateMatchCursor(snapshots3Input, 0, token.Token))
        {
            while (snapshots3Cursor.MoveNext(out var snapshots3Match))
                snapshots3.Add(snapshots3Match);
        }
        Assert.Equal(new[] { "alpha", "beta", "gamma" }, snapshots3.Select(captureSnapshot3 => System.Text.Encoding.UTF8.GetString(captureSnapshot3.GetGroupMemory(captureSnapshot3.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        var collectedRanges4 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor4 = regex.CreateMatchCursor(input, 0, token.Token))
        {
            while (rangeCursor4.MoveNext(out var rangeMatch4))
                collectedRanges4.Add(rangeMatch4);
        }
        var matches = collectedRanges4.ToArray();
        var first = matches[0];
        Assert.Equal(Encoding.UTF8.GetByteCount(prefix), first.Value.StartOffsetInBytes);
        Assert.Equal("alpha", System.Text.Encoding.UTF8.GetString(first.GetGroupMemory(1).Span));
        Assert.Equal(1, first.LastIndex);
        ReadOnlyMemory<byte> snapshots4Input = input;
        int snapshots4Start = matches[1].Value.StartOffsetInBytes;
        var snapshots4 = new List<Utf8PythonRangeMatch>();
        using (var snapshots4Cursor = regex.CreateMatchCursor(snapshots4Input, snapshots4Start, token.Token))
        {
            while (snapshots4Cursor.MoveNext(out var snapshots4Match))
                snapshots4.Add(snapshots4Match);
        }
        Assert.Equal(new[] { "beta", "gamma" }, snapshots4.Select(captureSnapshot4 => System.Text.Encoding.UTF8.GetString(captureSnapshot4.GetGroupMemory(captureSnapshot4.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        Assert.False(regex.Match(input).Success);
        Assert.False(regex.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(regex.Match(input, first.Value.StartOffsetInBytes).Success);
        Assert.ThrowsAny<ArgumentException>(() =>
        {
            ReadOnlyMemory<byte> snapshots5Input = input;
            int snapshots5Start = padding + 1;
            var snapshots5 = new List<Utf8PythonRangeMatch>();
            using (var snapshots5Cursor = regex.CreateMatchCursor(snapshots5Input, snapshots5Start, default))
            {
                while (snapshots5Cursor.MoveNext(out var snapshots5Match))
                    snapshots5.Add(snapshots5Match);
            }
            return snapshots5;
        });
        Assert.Equal("alpha", System.Text.Encoding.UTF8.GetString(first.GetGroupMemory(1).Span));
    }

    [Theory]
    [InlineData("([a-z]{2,3})!", "a9bb!ccc!dddd!", new[] { "bb", "ccc", "ddd" })]
    [InlineData("(?i)([a-z]+)", "9KſİıX", new[] { "KſİıX" })]
    [InlineData("(?ai)([a-z]+)", "9KſİıX", new[] { "X" })]
    [InlineData("([^a-z]+)", "abc😀12", new[] { "😀12" })]
    [InlineData(@"([\x00-\x7f]+)", "😀\0\u007fX𐐀", new[] { "\0\u007fX" })]
    [InlineData(@"([\x00-\x3f]+)", "😀\0?𐐀@", new[] { "\0?" })]
    [InlineData(@"([\x3f-\x40]+)", "9?@AX", new[] { "?@" })]
    [InlineData(@"([a-z]+)-\1", "99abc-abc xx", new[] { "abc" })]
    [InlineData("([a-z]+)(?=pha)", "9alpha", new[] { "al" })]
    [InlineData("([A-Za-z_][A-Za-z0-9_]*)", "99[\\]^`😀_id2 A3 z", new[] { "_id2", "A3", "z" })]
    [InlineData("([ACE]+)", "BDACEBDE", new[] { "ACE", "E" })]
    [InlineData(@"([a-cx-z]+)-\1", "mno ab-ab xyz-xyz", new[] { "ab", "xyz" })]
    public void VerificationRetainsMinimumTailNegationAndFolding(string pattern, string text, string[] expected)
    {
        using var regex = new Utf8PythonRegex(pattern);
        using var cursor = regex.CreateMatchCursor(Encoding.UTF8.GetBytes(text));
        var values = new List<string>();
        while (cursor.MoveNext(out var match))
            values.Add(Encoding.UTF8.GetString(match.GetGroupMemory(1).Span));
        Assert.Equal(expected, values);
    }

    [Fact]
    public void ImpossibleStartsStillRespectOutputAdmission()
    {
        foreach (var pattern in new[] { Pattern, "(,)?;", "(?>(?P<word>[a-z]+))" })
        {
            foreach (var resultLimit in new[] { true, false })
            {
                var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
                {
                    MatchTimeout = Timeout.InfiniteTimeSpan,
                    ResourcePolicy = resultLimit ? new()
                    {
                        MaxResultBytes = 1
                    }

        : new()
        {
            MaxWorkingBytes = 1
        }
                });
                var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                {
                    ReadOnlyMemory<byte> snapshots6Input = ("999 alpha,;"u8).ToArray();
                    var snapshots6 = new List<Utf8PythonRangeMatch>();
                    using (var snapshots6Cursor = regex.CreateMatchCursor(snapshots6Input, 0, default))
                    {
                        while (snapshots6Cursor.MoveNext(out var snapshots6Match))
                            snapshots6.Add(snapshots6Match);
                    }
                    return snapshots6;
                });
                Assert.Equal(resultLimit ? Utf8ResourceLimitKind.ExecutionResultBytes : Utf8ResourceLimitKind.ExecutionWorkingBytes, failure.ResourceKind);
            }
        }
    }

    [Theory]
    [InlineData(Pattern)]
    [InlineData("(?>(?P<word>[a-z]+))")]
    public void DurableAliasesSharePositionAndRetainedCapturesSurviveEofAndDisposal(string pattern)
    {
        Utf8PythonRangeMatch cursorRange = default;
        var regex = new Utf8PythonRegex(pattern);
        using var cursor = regex.CreateMatchCursor(Encoding.UTF8.GetBytes("999😀alpha 123 beta"));
        var alias = cursor;
        Assert.True(cursor.MoveNext(out cursorRange));
        var first = cursorRange;
        Assert.True(alias.MoveNext(out cursorRange));
        Assert.Equal("beta", System.Text.Encoding.UTF8.GetString(cursorRange.GetGroupMemory(1).Span));
        Assert.False(cursor.MoveNext(out cursorRange));
        alias.Dispose();
        Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext(out _));
        Assert.Equal("alpha", System.Text.Encoding.UTF8.GetString(first.GetGroupMemory(1).Span));
        Assert.Equal(7, first.Value.StartOffsetInBytes);
        Assert.Equal(1, first.LastIndex);
    }

    [Fact]
    public void LaterCapturesKeepTheirValuesAndCompletionOrder()
    {
        var regex = new Utf8PythonRegex("([a-z]+)-([0-9]+)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 100000,
                MaxDepth = 32,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            }
        });
        var input = "999😀one-12 z-3 bad!"u8;
        ReadOnlyMemory<byte> snapshots7Input = (input).ToArray();
        var snapshots7 = new List<Utf8PythonRangeMatch>();
        using (var snapshots7Cursor = regex.CreateMatchCursor(snapshots7Input, 0, default))
        {
            while (snapshots7Cursor.MoveNext(out var snapshots7Match))
                snapshots7.Add(snapshots7Match);
        }
        var strings = snapshots7.Select(captureSnapshot5 => Enumerable.Range(1, captureSnapshot5.Groups.Length - 1).Select(captureNumber5 => System.Text.Encoding.UTF8.GetString(captureSnapshot5.GetGroupMemory(captureNumber5).Span)).ToArray()).ToArray();
        ReadOnlyMemory<byte> snapshots8Input = (input).ToArray();
        var snapshots8 = new List<Utf8PythonRangeMatch>();
        using (var snapshots8Cursor = regex.CreateMatchCursor(snapshots8Input, 0, default))
        {
            while (snapshots8Cursor.MoveNext(out var snapshots8Match))
                snapshots8.Add(snapshots8Match);
        }
        var bytes = snapshots8.Select(captureSnapshot6 => Enumerable.Range(1, captureSnapshot6.Groups.Length - 1).Select(captureNumber6 => captureSnapshot6.GetGroupMemory(captureNumber6).ToArray()).ToArray()).ToArray();
        Assert.Equal(2, strings.Length);
        Assert.Equal(2, bytes.Length);
        Assert.Equal(new[] { "one", "12" }, strings[0]);
        Assert.Equal(new[] { "z", "3" }, strings[1]);
        for (var index = 0; index < strings.Length; index++)
        {
            Assert.Equal(strings[index], bytes[index].Select(Encoding.UTF8.GetString));
        }
        var collectedRanges5 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor5 = regex.CreateMatchCursor((input).ToArray(), 0, default))
        {
            while (rangeCursor5.MoveNext(out var rangeMatch5))
                collectedRanges5.Add(rangeMatch5);
        }
        Assert.All(collectedRanges5.ToArray(), match => Assert.Equal(2, match.LastIndex));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void WorkDenialInCandidateSearchKeepsItsAuthorityAndReleasesStorage(int kind)
    {
        var pattern = kind switch
        {
            1 => @"\b(?P<digits>\d+)\b",
            2 => "(,)?;",
            3 => "(?P<word>[éa]+)",
            _ => Pattern
        };
        var program = Compile(pattern);
        var input = Encoding.UTF8.GetBytes(new string(kind > 0 ? 'x' : '9', 8192) +
            (kind == 1 ? " 123" : kind == 2 ? ",;" : kind == 3 ? "aaa" : "alpha"));
        var probe = new PythonReScalarMatchCursor(input, program, 0, Timeout.InfiniteTimeSpan,
            null, pattern, new(Limits, Timeout.InfiniteTimeSpan, default));
        var constructionWork = probe.WorkingInvocation.Budget.Steps;
        probe.Dispose();
        var cursor = new PythonReScalarMatchCursor(input, program, 0, Timeout.InfiniteTimeSpan,
            null, pattern, new(new(constructionWork + 1, 32, 1 << 20, 1 << 20), Timeout.InfiniteTimeSpan, default));
        Exception? error = null;
        try
        {
            cursor.MoveNext();
        }
        catch (Exception failure)
        {
            error = failure;
        }
        finally
        {
            cursor.Dispose();
        }
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
        Assert.Equal(0UL, cursor.ReservedBytes);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    [InlineData(false, 3)]
    [InlineData(true, 3)]
    [InlineData(false, 4)]
    [InlineData(true, 4)]
    [InlineData(false, 5)]
    [InlineData(true, 5)]
    public void CandidateBlocksObserveOriginalTokenAndDeadlineAndKeepParentLoans(bool expire, int kind)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var timeout = expire ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan;
        var pattern = kind switch
        {
            1 => "(?P<word>[A-Za-z_]+)",
            2 => @"\b(?P<digits>\d+)\b",
            3 => "(?P<sep>,)?;",
            4 => "(?>ab+)c",
            5 => "(?P<word>[éa]+)",
            _ => Pattern
        };
        var cursor = new PythonReScalarMatchCursor(Encoding.UTF8.GetBytes(new string(kind is 2 or 3 ? 'x' : '9', 200000) +
            (kind == 2 ? " 123" : kind == 3 ? ",;" : kind == 4 ? "abbbc" : kind == 5 ? "aaa" : "alpha")),
            Compile(pattern), 0, timeout,
            null, pattern, new(Limits, timeout, cancellation.Token));
        Assert.True(cursor.WorkingInvocation.Ledger.TryReserve(13));
        cursor.WorkingInvocation.ShareStorageForCursor();
        var observer = cursor.WorkingInvocation;
        var initialBytes = cursor.ReservedBytes;
        while ((cursor.WorkingInvocation.Budget.Steps & 63) != 62)
            Assert.Equal(Utf8ExecutionLimitKind.None, cursor.WorkingInvocation.Budget.TryChargeStep());
        var entered = false;
        cursor.WorkingInvocation.CheckpointHook = _ =>
        {
            entered = true;
            Assert.Equal(initialBytes, observer.Ledger.ReservedBytes); // No candidate entered the VM.
            if (expire)
            {
                ticks = 50;
            }
            else
            {
                cancellation.Cancel();
            }
        };
        Exception? error = null;
        try
        {
            cursor.MoveNext();
        }
        catch (Exception failure)
        {
            error = failure;
        }
        finally
        {
            cursor.Dispose();
        }
        Assert.True(entered);
        if (expire)
        {
            var failure = Assert.IsType<Utf8ExecutionDeadlineExpiredException>(error);
        }
        else
        {
            Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
        }
        Assert.Equal(13UL, cursor.ReservedBytes);
    }
}
