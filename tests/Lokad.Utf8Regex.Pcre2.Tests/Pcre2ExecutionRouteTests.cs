using System.Buffers;
using System.Text;

using global::Lokad.Utf8Regex;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ExecutionRouteTests
{
    [Fact]
    public void ExactLiteralPublishesNonoverlappingByteRanges()
    {
        var regex = new Utf8Pcre2Regex("foo");

        Assert.True(regex.IsMatch("xxfooyy"u8));
        Assert.Equal(2, regex.Count("foo xx foo"u8));

        var match = regex.Match("xxfooyy"u8);
        Assert.True(match.Success);
        Assert.Equal(2, match.StartOffsetInBytes);
        Assert.Equal(5, match.EndOffsetInBytes);

        var enumerator = regex.EnumerateMatches("foo xx foo"u8);
        Assert.True(enumerator.MoveNext());
        Assert.Equal((0, 3), (enumerator.Current.StartOffsetInBytes, enumerator.Current.EndOffsetInBytes));
        Assert.True(enumerator.MoveNext());
        Assert.Equal((7, 10), (enumerator.Current.StartOffsetInBytes, enumerator.Current.EndOffsetInBytes));
        Assert.False(enumerator.MoveNext());
    }

    [Theory]
    [InlineData("a|ab", "ab a", 0, new[] { 0, 1, 3, 4 })]
    [InlineData("ab|a", "ab a", 0, new[] { 0, 2, 3, 4 })]
    [InlineData("foo|foo", "foo foo", 0, new[] { 0, 3, 4, 7 })]
    [InlineData("Tom|Sawyer|Huckleberry|Finn", "Tom x Finn x Sawyer", 0, new[] { 0, 3, 6, 10, 13, 19 })]
    [InlineData("café|naïve", "é café naïve café", 3, new[] { 3, 8, 9, 15, 16, 21 })]
    [InlineData("é|😀", "x é 😀 é", 0, new[] { 2, 4, 5, 9, 10, 12 })]
    public void LiteralFamilyOperationsRespectBranchOrderAndByteStarts(
        string pattern, string inputText, int startOffsetInBytes, int[] expectedEndpoints)
    {
        using var regex = new Utf8Pcre2Regex(pattern);
        var inputBytes = Encoding.UTF8.GetBytes(inputText);
        Assert.True(regex.IsMatch(inputBytes, startOffsetInBytes));
        Assert.Equal(expectedEndpoints.Length / 2, regex.Count(inputBytes, startOffsetInBytes));
        var first = regex.Match(inputBytes, startOffsetInBytes);
        Assert.True(first.Success);
        Assert.Equal((expectedEndpoints[0], expectedEndpoints[1]), (first.StartOffsetInBytes, first.EndOffsetInBytes));

        var cursor = regex.EnumerateMatches(inputBytes, startOffsetInBytes);
        for (var i = 0; i < expectedEndpoints.Length; i += 2)
        {
            Assert.True(cursor.MoveNext());
            Assert.Equal((expectedEndpoints[i], expectedEndpoints[i + 1]),
                (cursor.Current.StartOffsetInBytes, cursor.Current.EndOffsetInBytes));
        }
        Assert.False(cursor.MoveNext());
    }

    [Theory]
    [InlineData("a|ab", "ab a", 2)]
    [InlineData("alpha|amber|bravo", "alpha amber bravo", 3)]
    [InlineData("Sherlock Holmes|John Watson|Irene Adler|Inspector Lestrade|Professor Moriarty", "Sherlock Holmes x Professor Moriarty", 2)]
    [InlineData("夏洛克·福尔摩斯|约翰华生|阿德勒|雷斯垂德|莫里亚蒂教授", "夏洛克·福尔摩斯 x 莫里亚蒂教授", 2)]
    [InlineData("Task|ValueTask|IAsyncEnumerable", "Task ValueTask IAsyncEnumerable", 3)]
    public void LiteralFamilyOperationsRespectBorrowedSliceAndStart(string pattern, string input, int expectedCount)
    {
        using var regex = new Utf8Pcre2Regex(pattern);
        var textBytes = Encoding.UTF8.GetBytes("é😀 " + input + " 中");
        byte[] storage = [0xff, .. textBytes, 0xff];
        var view = storage.AsSpan(1, textBytes.Length);
        Assert.Equal(expectedCount, regex.Count(view, 7));
        Assert.Equal(1, regex.Count(view, 7, Pcre2MatchOptions.Anchored));
        var enumerator = regex.EnumerateMatches(view, 7);
        var count = 0;
        while (enumerator.MoveNext())
        {
            Assert.True(enumerator.Current.HasContiguousByteRange);
            Assert.True(enumerator.Current.IsUtf8SliceWellFormed);
            count++;
        }

        Assert.Equal(expectedCount, count);
    }

    [Fact]
    public void LiteralFamilyCursorFeedsRangesAndByteTemplates()
    {
        var regex = new Utf8Pcre2Regex("tempus|magna|semper");
        var input = "é tempus magna semper tempus"u8;

        Assert.Equal(4, regex.Count(input));
        var enumerator = regex.EnumerateMatches(input);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(3, enumerator.Current.StartOffsetInBytes);

        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        Assert.Equal(16, enumerator.Current.StartOffsetInBytes);

        const string expected = "é <tempus> <magna> <semper> <tempus>";
        Assert.Equal(expected, Encoding.UTF8.GetString(regex.Replace(input, "<$0>"u8)));

        var expectedFixed = "é x x x x"u8;
        Span<byte> destination = stackalloc byte[expectedFixed.Length];
        Assert.Equal(OperationStatus.Done, regex.TryReplace(input, "x"u8, destination, out var bytesWritten));
        Assert.Equal(expectedFixed.Length, bytesWritten);
        Assert.True(destination.SequenceEqual(expectedFixed));

        Span<byte> tooSmall = stackalloc byte[expectedFixed.Length - 1];
        Assert.Equal(
            OperationStatus.DestinationTooSmall,
            regex.TryReplace(
                input,
                "x"u8,
                tooSmall,
                out bytesWritten,
substitutionOptions: Pcre2SubstitutionOptions.SubstituteOverflowLength));
        Assert.Equal(expectedFixed.Length, bytesWritten);
    }

    [Fact]
    public void LiteralFamilyCountHonorsStartOffsetsAndFallsBackForRuntimeOptions()
    {
        var regex = new Utf8Pcre2Regex("foo|bar");
        var input = "xfoo bar"u8;

        Assert.Equal(1, regex.Count(input, 4));
        Assert.Equal(0, regex.Count(input, 0, Pcre2MatchOptions.Anchored));
        Assert.Equal(1, regex.Count(input, 1, Pcre2MatchOptions.Anchored));
        Assert.False(regex.IsMatch(input, 0, Pcre2MatchOptions.Anchored));
        Assert.True(regex.IsMatch(input, 1, Pcre2MatchOptions.Anchored));

        var anchoredMiss = regex.EnumerateMatches(input, 0, Pcre2MatchOptions.Anchored);
        Assert.False(anchoredMiss.MoveNext());
        var anchoredMatch = regex.EnumerateMatches(input, 1, Pcre2MatchOptions.Anchored);
        Assert.True(anchoredMatch.MoveNext());
        Assert.Equal("foo", System.Text.Encoding.UTF8.GetString(anchoredMatch.Current.GetValueBytes()));
        Assert.False(anchoredMatch.MoveNext());
    }

    [Fact]
    public void LiteralFamilyCountHandlesEmptyAlternativesAndCompileAnchors()
    {
        var empty = new Utf8Pcre2Regex("foo|");
        var anchored = new Utf8Pcre2Regex("foo|bar", options: Pcre2CompileOptions.Anchored);

        Assert.Equal(3, empty.Count("xfoo"u8));
        Assert.Equal(0, anchored.Count("xfoo"u8));
    }

    [Fact]
    public void OptionalNamedGroupPreservesCountAndTemplateExpansion()
    {
        var regex = new Utf8Pcre2Regex("foo(?<Bar>BAR)?");

        Assert.Equal(2, regex.Count("foo fooBAR x"u8));
        var replaced = regex.Replace("fooBAR x"u8, "<$0|$Bar>"u8);

        Assert.Equal("<fooBAR|BAR> x", Encoding.UTF8.GetString(replaced));
    }

    [Fact]
    public void AbPlusPatternPreservesGlobalAndCaptureOperations()
    {
        var regex = new Utf8Pcre2Regex("(a)b+");

        Assert.False(regex.DebugUsesUtf8RegexTranslation);
        Assert.True(regex.IsMatch("xxabbbzz"u8));
        Assert.Equal(2, regex.Count("ab abb"u8));

        var probe = regex.Probe("a"u8, Pcre2PartialMode.Hard);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, probe.Kind);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(probe.GetPartial().Value.GetValueBytes()));
    }

    [Fact]
    public void AsciiQuantifiedPatternUsesBoundedIsMatchAndVmForGlobalOperations()
    {
        var regex = new Utf8Pcre2Regex(@"\s[a-zA-Z]{0,12}ing\s");

        Assert.False(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal(3, regex.Count(" sing  bringing  going  go "u8));
    }

    [Fact]
    public void OrderedWindowPublicCountPreservesDisjointMatches()
    {
        var regex = new Utf8Pcre2Regex("Tom.{10,25}river|river.{10,25}Tom");
        Assert.Equal(2, regex.Count("Tom and Becky near the river xx river beside old Tom"u8));
    }

    [Fact]
    public void LeadingAsciiWordBoundaryRunSearchPreservesPcre2WordSemantics()
    {
        var regex = new Utf8Pcre2Regex(@"\b\w{10,}\b");
        var input = Encoding.UTF8.GetBytes("short abcdefghij x_123456789 yyyyyyyyyy éabcdefghij");

        Assert.Equal(Pcre2CandidateSearchKind.LeadingAsciiWordBoundaryRun, regex.DebugCompiledProgram.CandidateSearch.Kind);
        Assert.Equal(4, regex.Count(input));
        Assert.Equal(3, regex.Count(input, 8));
        Assert.Equal("abcdefghij", System.Text.Encoding.UTF8.GetString(regex.Match(input).GetValueBytes()));

        Assert.Equal(
            "short <abcdefghij> <x_123456789> <yyyyyyyyyy> é<abcdefghij>",
            System.Text.Encoding.UTF8.GetString(regex.Replace(input, "<$0>"u8)));

        var compiled = regex.DebugCompiledProgram;
        var program = Assert.IsType<Pcre2BacktrackingDirectProgram>(compiled.Operations.Enumerate).Program;
        ReadOnlySpan<byte> subject = input;
        var cursor = Pcre2GlobalMatchCursor.CreateBacktracking(program, compiled.CandidateSearchPlan,
            subject, Utf8BytePosition.CreateScalarChecked(subject, 0, "startOffsetInBytes"), Pcre2MatchOptions.None,
            compiled.Request, collectDiagnostics: true);
        var count = 0;
        while (cursor.MoveNext()) count = checked(count + 1);
        var diagnostics = (Count: count, Execution: cursor.Diagnostics);
        Assert.Equal(4, diagnostics.Count);
        Assert.Equal(4UL, diagnostics.Execution.CandidateAttempts);
    }

    [Theory]
    [InlineData(@"\B\w{10,}\b", Pcre2CompileOptions.None)]
    [InlineData(@"\b\w+\b", Pcre2CompileOptions.None)]
    [InlineData(@"(\b\w{10,}\b)", Pcre2CompileOptions.None)]
    [InlineData(@"\b\w{10,}\b", Pcre2CompileOptions.Ucp)]
    [InlineData(@"\b\w{10,}\b", Pcre2CompileOptions.Caseless)]
    [InlineData(@"\w{10,}", Pcre2CompileOptions.Ucp)]
    [InlineData(@"(?i:\w{10,})", Pcre2CompileOptions.None)]
    public void LeadingAsciiWordRunSearchRejectsBroaderSemantics(
        string pattern,
        Pcre2CompileOptions options)
    {
        var regex = new Utf8Pcre2Regex(pattern, options: options);

        Assert.NotEqual(Pcre2CandidateSearchKind.LeadingAsciiWordBoundaryRun, regex.DebugCompiledProgram.CandidateSearch.Kind);
        Assert.NotEqual(Pcre2CandidateSearchKind.LeadingAsciiWordRun, regex.DebugCompiledProgram.CandidateSearch.Kind);
    }

    [Fact]
    public void LeadingAsciiWordRunSearchRetriesInsideTheSameWord()
    {
        using var regex = new Utf8Pcre2Regex(@"\w{2}(?=!)");
        var input = "é😀abcde! _xyz!"u8;
        Assert.Equal(Pcre2CandidateSearchKind.LeadingAsciiWordRun, regex.DebugCompiledProgram.CandidateSearch.Kind);
        var match = regex.Match(input, 7);
        Assert.Equal((9, 11), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.Equal(2, regex.Count(input, 7));
        Assert.Equal(1, regex.Count(input, 10));
        using var source = new CancellationTokenSource();
        Assert.Equal(2, regex.Count(input, 7, cancellationToken: source.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LeadingAsciiWordRunScanChargesWorkAndChecksInterruption(bool cancel)
    {
        const string pattern = @"\w{10,}";
        var input = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("abc ", 8192)));
        using var limited = new Utf8Pcre2Regex(pattern, policy: new()
        {
            ResourcePolicy = new() { MaxDepth = 4096 },
        });

        using var regex = new Utf8Pcre2Regex(pattern, policy: new()
        {
            ResourcePolicy = new() { MaxDepth = 4096 },
            MatchTimeout = TimeSpan.FromMilliseconds(20),
        });
        var reads = 0;
        var interrupt = false;
        using var source = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (!interrupt || ++reads < 10) return 0;
            if (!cancel) return 25;
            source.Cancel();
            return 0;
        });
        Assert.Equal(0, regex.Count(input, cancellationToken: source.Token));
        interrupt = true;
        if (cancel)
        {
            var failure = Assert.Throws<OperationCanceledException>(() => regex.Count(input, cancellationToken: source.Token));
            Assert.Equal(source.Token, failure.CancellationToken);
        }
        else Assert.Equal(Pcre2ErrorKind.Timeout,
            Assert.Throws<Pcre2MatchException>(() => regex.Count(input)).ErrorKind);
    }

    [Fact]
    public void LeadingAsciiWordBoundaryRunSearchSupportsConcurrentRegexReuse()
    {
        var regex = new Utf8Pcre2Regex(@"\b\w{10,}\b");
        var input = "short abcdefghij klmnopqrst"u8.ToArray();

        Parallel.For(0, 256, _ =>
        {
            Assert.Equal(2, regex.Count(input));
            Assert.Equal("short <abcdefghij> <klmnopqrst>", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "<$0>"u8)));
        });
    }

    [Fact]
    public void LeadingAsciiWordRunSearchMatchesVmOnlyGeneratedCases()
    {
        string[] patterns =
        [
            @"\b\w{2,}\b",
            @"\b\w{3,5}\b",
            @"\b\w{4,}?\b",
            @"\b\w{4,}+\b",
            @"\b\w{3,}\B",
            @"\w{3,5}",
            @"\w{4,}?",
            @"\w{4,}+",
            @"\w{2}(?=!)",
        ];
        string[] fragments = ["a", "Z", "0", "_", " ", "-", ".", "!", "\n", "é", "😀"];
        var random = new Random(0x5EED);
        foreach (var pattern in patterns)
        {
            var candidate = new Utf8Pcre2Regex(pattern);
            var vmOnly = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.NoAutoCapture);
            var kind = pattern.StartsWith(@"\b", StringComparison.Ordinal)
                ? Pcre2CandidateSearchKind.LeadingAsciiWordBoundaryRun : Pcre2CandidateSearchKind.LeadingAsciiWordRun;
            Assert.Equal(kind, candidate.DebugCompiledProgram.CandidateSearch.Kind);
            Assert.Equal(Pcre2CandidateSearchKind.None, vmOnly.DebugCompiledProgram.CandidateSearch.Kind);

            for (var caseIndex = 0; caseIndex < 128; caseIndex++)
            {
                var builder = new StringBuilder();
                var length = random.Next(64);
                for (var i = 0; i < length; i++)
                {
                    builder.Append(fragments[random.Next(fragments.Length)]);
                }

                var input = Encoding.UTF8.GetBytes(builder.ToString());
                Assert.Equal(GetRanges(vmOnly, input), GetRanges(candidate, input));
                Assert.Equal(vmOnly.Count(input), candidate.Count(input));
                Assert.Equal(System.Text.Encoding.UTF8.GetString(vmOnly.Replace(input, "<$0>"u8)), System.Text.Encoding.UTF8.GetString(candidate.Replace(input, "<$0>"u8)));
            }
        }

        static List<(int Start, int End)> GetRanges(Utf8Pcre2Regex regex, byte[] input)
        {
            var ranges = new List<(int Start, int End)>();
            var enumerator = regex.EnumerateMatches(input);
            while (enumerator.MoveNext())
            {
                ranges.Add((enumerator.Current.StartOffsetInBytes, enumerator.Current.EndOffsetInBytes));
            }

            return ranges;
        }
    }

    [Theory]
    [InlineData(@"(?<=x)foo")]
    [InlineData(@"foo\Kbar")]
    [InlineData(@"\1(foo)")]
    [InlineData(@"(?R)|foo")]
    public void UnsafeStartSemanticsStayOffTheCandidateSearchPath(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        Assert.Equal(Pcre2CandidateSearchKind.None, regex.DebugCompiledProgram.CandidateSearch.Kind);
    }

    [Fact]
    public void NecessarySingletonPrefixStillVerifiesTheFollowingBackreference()
    {
        var regex = new Utf8Pcre2Regex(@"(foo)\1");
        Assert.Equal(Pcre2CandidateSearchKind.LeadingAsciiSet, regex.DebugCompiledProgram.CandidateSearch.Kind);
        Assert.Equal(2, regex.Count("foofoo foo bar foofoo"u8));
        Assert.False(regex.IsMatch("foo"u8));
        ReadOnlySpan<byte> result1Input = "xx foofoo"u8;
        var result1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var result1Result = regex.Match(result1Input, captures: result1Captures);
        Assert.Equal("foo", System.Text.Encoding.UTF8.GetString((result1Captures[1].Success ? result1Input.Slice(result1Captures[1].StartOffsetInBytes, result1Captures[1].EndOffsetInBytes - result1Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Theory]
    [InlineData(@"[a-z]+@[a-z]+", "xx foo@bar yy", 1)]
    [InlineData(@"[\w]+://[^/\s?#]+", "xx https://example.test yy", 1)]
    [InlineData(@"\w+\s+Holmes", "xx Sherlock Holmes yy Holmes", 2)]
    public void LeadingRunRequiredLiteralUsesMonotoneCandidateSearch(
        string pattern,
        string input,
        int expectedCount)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Pcre2CandidateSearchKind.LeadingRunThenLiteral, regex.DebugCompiledProgram.CandidateSearch.Kind);
        Assert.Equal(expectedCount, regex.Count(bytes));
    }

    [Theory]
    [InlineData(@"[^a]+\.[^z]+", "This regex has no delimiter", false)]
    [InlineData(@"[^a]+\.[^z]+", "..x", true)]
    [InlineData(@"[^a]+?\.[^z]+", "bc.de", true)]
    [InlineData(@"[^a]++\.[^z]+", "bc.de", false)]
    public void LeadingRunCanConsumeTheRequiredLiteralWithoutDisablingCandidateSearch(
        string pattern,
        string input,
        bool expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        Assert.Equal(Pcre2CandidateSearchKind.LeadingRunThenLiteral, regex.DebugCompiledProgram.CandidateSearch.Kind);
        Assert.Equal(expected, regex.IsMatch(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void OverlappingLeadingRunCandidateSearchFeedsAllGlobalOperations()
    {
        var regex = new Utf8Pcre2Regex(@"[^a]+\.[^z]+");
        var input = "bc.dzabc.ezaβ.γz"u8;

        Assert.Equal(3, regex.Count(input));

        var enumerator = regex.EnumerateMatches(input);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("bc.d", System.Text.Encoding.UTF8.GetString(enumerator.Current.GetValueBytes()));
        Assert.True(enumerator.MoveNext());
        Assert.Equal("bc.e", System.Text.Encoding.UTF8.GetString(enumerator.Current.GetValueBytes()));
        Assert.True(enumerator.MoveNext());
        Assert.Equal("β.γ", System.Text.Encoding.UTF8.GetString(enumerator.Current.GetValueBytes()));
        Assert.False(enumerator.MoveNext());

        Assert.Equal("<bc.d>za<bc.e>za<β.γ>z", Encoding.UTF8.GetString(regex.Replace(input, "<$0>"u8)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LeadingRunCandidateSearchRespectsAStartInsideTheRun(bool funded)
    {
        using var regex = new Utf8Pcre2Regex(@"[a-z]+@[a-z]+", policy: funded ? new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxDepth = 64, MaxWorkingBytes = 4096 }
        } : null);
        using var source = new CancellationTokenSource();
        var input = "😀 foo@bar"u8;

        var match = regex.Match(input, startOffsetInBytes: 6, cancellationToken: funded ? source.Token : default);

        Assert.True(match.Success);
        Assert.Equal(6, match.StartOffsetInBytes);
        Assert.Equal("oo@bar", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
    }

    [Fact]
    public void BooleanSliceSearchSkipsRejectedDelimiterRunsUnderAFiniteWorkLimit()
    {
        using var regex = new Utf8Pcre2Regex(@"[a-z]+@(?<tail>[a-z]+)", policy: new()
        {
            ResourcePolicy = new() { MaxWorkingBytes = 4096 },
        });
        var prefix = "😀" + new string('a', 2048);
        var rejected = prefix + "@!";
        const int floor = 4;

        Assert.Equal(Pcre2CandidateSearchKind.LeadingRunThenLiteral, regex.DebugCompiledProgram.CandidateSearch.Kind);
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
        Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes(rejected), startOffsetInBytes: floor));
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(rejected + "𐐨abc@tail!"), startOffsetInBytes: floor));
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(prefix + "@tail!"), startOffsetInBytes: floor));
        Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes(rejected + "abc@!@tail!"), startOffsetInBytes: floor));
    }

    [Fact]
    public void LeadingRunRewindKeepsTheUtf8FloorAndUnicodeClassMembership()
    {
        using var unicode = new Utf8Pcre2Regex(@"[\p{L}]+@(?<tail>[a-z]+)", policy: new()
        {
            ResourcePolicy = new() { MaxWorkingBytes = 4096 },
        });
        using var ascii = new Utf8Pcre2Regex(@"\w+@(?<tail>[a-z]+)");
        var input = "!!βK𐐨@tail!"u8;
        var floor = "!!β"u8.Length;
        var tail = "!!βK𐐨@"u8.Length;
        Span<Utf8Pcre2CaptureRange> captures = stackalloc Utf8Pcre2CaptureRange[2];

        Assert.Equal(Pcre2CandidateSearchKind.LeadingRunThenLiteral, unicode.DebugCompiledProgram.CandidateSearch.Kind);
        var result = unicode.Match(input, startOffsetInBytes: floor, captures: captures);
        Assert.True(result.Success);
        Assert.Equal((floor, input.Length - 1), (result.StartOffsetInBytes, result.EndOffsetInBytes));
        Assert.Equal((tail, input.Length - 1), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
        Assert.False(unicode.IsMatch(input, startOffsetInBytes: tail - 1));
        Assert.False(ascii.IsMatch(input, startOffsetInBytes: floor));
    }

    [Fact]
    public void FundedRunDelimiterMissNeedsNoAttemptWorkspace()
    {
        using var regex = new Utf8Pcre2Regex(@"[\w]+://[^/\s?#]+[^\s?#]+", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkingBytes = 1 }
        });
        using var source = new CancellationTokenSource();
        Assert.Equal(Pcre2CandidateSearchKind.LeadingRunThenLiteral, regex.DebugCompiledProgram.CandidateSearch.Kind);
        Assert.False(regex.IsMatch("😀 word with no scheme delimiter"u8, cancellationToken: source.Token));
    }

    [Theory]
    [InlineData(@"[a-z]{1,3}@(?P<tail>[a-z]+)", "abcdef@z")]
    [InlineData(@"\w{1,3}\s+Holmes", "abcdef Holmes")]
    public void BoundedLeadingRunCanMatchFromInsideALongerRun(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        using var source = new CancellationTokenSource();
        foreach (var funded in new[] { false, true })
        {
            using var regex = new Utf8Pcre2Regex(pattern, policy: funded ? new Utf8RegexPolicy
            {
                ResourcePolicy = new() { MaxDepth = 64, MaxWorkingBytes = 4096 }
            } : null);
            var token = funded ? source.Token : default;
            Assert.NotEqual(Pcre2CandidateSearchKind.LeadingRunThenLiteral, regex.DebugCompiledProgram.CandidateSearch.Kind);
            var match = regex.Match(input, cancellationToken: token);
            Assert.True(match.Success);
            Assert.Equal((3, input.Length), (match.StartOffsetInBytes, match.EndOffsetInBytes));
            Assert.Equal(1, regex.Count(input, cancellationToken: token));
        }
    }

    [Fact]
    public void IpAddressPatternUsesABoundedDotWindowBeforePcre2Verification()
    {
        var regex = new Utf8Pcre2Regex(@"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])");

        Assert.Equal(Pcre2CandidateSearchKind.LeadingAsciiSetWithWindow, regex.DebugCompiledProgram.CandidateSearch.Kind);
        Assert.Equal(2, regex.Count("x 192.168.001.010 y 999.999.999.999 z 010.020.030.040"u8));
    }

    [Fact]
    public void BoundedRequiredLiteralWindowEnumeratesPossibleStartsInOrder()
    {
        var regex = new Utf8Pcre2Regex(@"\s[a-zA-Z]{0,12}ing\s");
        var input = "x sing  bringing  going  go "u8;

        Assert.Equal(Pcre2CandidateSearchKind.LeadingAsciiSetWithWindow, regex.DebugCompiledProgram.CandidateSearch.Kind);
        Assert.Equal(3, regex.Count(input));

        var match = regex.Match(input, startOffsetInBytes: 2);
        Assert.True(match.Success);
        Assert.Equal(" bringing ", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
    }

    [Fact]
    public void BoundedRequiredLiteralWindowSupportsWideAsciiPrefixes()
    {
        var regex = new Utf8Pcre2Regex(@"[\x00-\x7F]{1,2}foo");

        Assert.Equal(Pcre2CandidateSearchKind.BoundedLiteralWindow, regex.DebugCompiledProgram.CandidateSearch.Kind);
        Assert.Equal(2, regex.Count("xfoo yyfoo z"u8));
    }

    [Fact]
    public void DateValidatorPreservesBooleanAndCaptureOperations()
    {
        var regex = new Utf8Pcre2Regex(@"^\d{1,2}/\d{1,2}/\d{4}$");

        Assert.False(regex.DebugUsesUtf8RegexTranslation);
        Assert.True(regex.IsMatch("12/12/2001"u8));
        Assert.False(regex.IsMatch("12-12-2001"u8));
    }

    [Fact]
    public void CreditCardValidatorUsesOwnedOperationPrograms()
    {
        var regex = new Utf8Pcre2Regex(@"(\d{4}[- ]){3}\d{3,4}");

        Assert.False(regex.DebugUsesUtf8RegexTranslation);
        Assert.True(regex.IsMatch("1234-5678-1234-456"u8));
        Assert.False(regex.IsMatch("1234-5678"u8));
    }

    [Fact]
    public void FloatValidatorUsesOwnedOperationPrograms()
    {
        var regex = new Utf8Pcre2Regex(@"^[-+]?\d*\.?\d*$");

        Assert.False(regex.DebugUsesUtf8RegexTranslation);
        Assert.True(regex.IsMatch("-3.14159"u8));
        Assert.False(regex.IsMatch("3.14.159"u8));
    }

    [Fact]
    public void DateSearchUsesOwnedOperationPrograms()
    {
        var regex = new Utf8Pcre2Regex(@"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b");

        Assert.False(regex.DebugUsesUtf8RegexTranslation);
        Assert.True(regex.IsMatch("today is 12/12/2001 indeed"u8));
        Assert.False(regex.IsMatch("today is 12-12-2001 indeed"u8));
    }

    [Fact]
    public void IpSearchUsesOwnedOperationPrograms()
    {
        var regex = new Utf8Pcre2Regex(@"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])");

        Assert.True(regex.IsMatch("127.000.000.001"u8));
        Assert.False(regex.IsMatch("not an ip address"u8));
    }

    [Fact]
    public void UriSearchUsesOwnedOperationPrograms()
    {
        var regex = new Utf8Pcre2Regex(@"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?");

        Assert.False(regex.DebugUsesUtf8RegexTranslation);
        Assert.True(regex.IsMatch("http://example.com/path?q=1#frag"u8));
        Assert.False(regex.IsMatch("not a uri"u8));
    }

    [Fact]
    public void GreedyOptionalTailKeepsTheFullRangeAndOnlyRentsForPossibleRetries()
    {
        using var regex = new Utf8Pcre2Regex(@"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?", policy: new()
        {
            ResourcePolicy = new() { MaxWorkingBytes = 1 }
        });
        using var token = new CancellationTokenSource();
        var input = "😀 https://atlas.example.org/path?q=1#frag"u8;
        Assert.True(regex.IsMatch(input, cancellationToken: token.Token));
        var range = regex.Match(input, cancellationToken: token.Token);
        Assert.Equal((5, input.Length), (range.StartOffsetInBytes, range.EndOffsetInBytes));

        var retry = Assert.Throws<Pcre2MatchException>(() => regex.IsMatch("https://atlas.example.org"u8, cancellationToken: token.Token));
        Assert.Equal(Pcre2ErrorKind.HeapLimit, retry.ErrorKind);
        var anchored = Assert.Throws<Pcre2MatchException>(() => regex.IsMatch("https://atlas.example.org/path"u8,
            matchOptions: Pcre2MatchOptions.EndAnchored, cancellationToken: token.Token));
        Assert.Equal(Pcre2ErrorKind.HeapLimit, anchored.ErrorKind);
    }

    [Fact]
    public void GroupZeroOnlyPatternReturnsPrimaryByteRange()
    {
        var regex = new Utf8Pcre2Regex(@"\w{10,}");

        var match = regex.Match("abcdefghij short"u8);

        Assert.True(match.Success);
        Assert.Equal("abcdefghij", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.Equal(0, match.StartOffsetInBytes);
        Assert.Equal(10, match.EndOffsetInBytes);
    }

    [Fact]
    public void CapturedPatternPublishesNamedCursorRanges()
    {
        var regex = new Utf8Pcre2Regex("foo(?<Bar>BAR)?");
        ReadOnlyMemory<byte> match3Input = ("xxfooBARzz"u8).ToArray();
        using var match3Cursor = regex.CreateMatchCursor(match3Input, 2);
        match3Cursor.MoveNext(out var match3Result);
        match3Cursor.Dispose();

        var match = match3Result;

        Assert.True(match.Success);
        Assert.Equal("fooBAR", System.Text.Encoding.UTF8.GetString((match.Value.Success ? match3Input.Span.Slice(match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));

        var whole = match.Groups[0];
        Assert.True(whole.Success);
        Assert.Equal("fooBAR", System.Text.Encoding.UTF8.GetString((whole.Success ? match3Input.Span.Slice(whole.StartOffsetInBytes, whole.EndOffsetInBytes - whole.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));

        var capture = match.Groups[1];
        Assert.True(capture.Success);
        Assert.Equal("BAR", System.Text.Encoding.UTF8.GetString((capture.Success ? match3Input.Span.Slice(capture.StartOffsetInBytes, capture.EndOffsetInBytes - capture.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));

        Assert.True(match.TryGetGroup(1, out var named)); Assert.True(named.Success);
        Assert.True(named.Success);
        Assert.Equal("BAR", System.Text.Encoding.UTF8.GetString((named.Success ? match3Input.Span.Slice(named.StartOffsetInBytes, named.EndOffsetInBytes - named.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void LongWordCountUsesOwnedOperationPrograms()
    {
        var regex = new Utf8Pcre2Regex(@"\w{10,}");
        var input = "abcdefghij short klmnopqrst"u8;

        Assert.False(regex.DebugUsesUtf8RegexTranslation);
        Assert.True(regex.Match(input, startOffsetInBytes: 17).Success);
        Assert.True(regex.Match(input, startOffsetInBytes: 16).Success);
        Assert.True(regex.Match(input, startOffsetInBytes: 10).Success);
        Assert.Equal(2, regex.Count(input));
    }

    [Fact]
    public void BoundaryLongWordCountUsesOwnedOperationPrograms()
    {
        var regex = new Utf8Pcre2Regex(@"\b\w{10,}\b");

        Assert.False(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal(2, regex.Count("abcdefghij short klmnopqrst"u8));
    }

    [Fact]
    public void HolmesWordCountUsesOwnedOperationPrograms()
    {
        var regex = new Utf8Pcre2Regex(@"\w+\s+Holmes");

        Assert.False(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal(2, regex.Count("Mr Holmes and Sherlock Holmes"u8));
    }

    [Fact]
    public void NonNewlineCountUsesOwnedOperationPrograms()
    {
        var regex = new Utf8Pcre2Regex(@"[^\n]*");

        Assert.False(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal(4, regex.Count("abc\ndef"u8));
    }

    [Fact]
    public void FtpLineValidatorUsesOwnedOperationPrograms()
    {
        var regex = new Utf8Pcre2Regex(@"^([0-9]+)(\-| |$)(.*)$");

        Assert.True(regex.IsMatch("100- this is a line of ftp response"u8));
        Assert.False(regex.IsMatch("ftp response without code"u8));
    }

    [Fact]
    public void EmailPatternUsesOwnedOperationPrograms()
    {
        var regex = new Utf8Pcre2Regex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}");

        Assert.False(regex.DebugUsesUtf8RegexTranslation);
        Assert.DoesNotContain("ManagedRegex", regex.DebugDescribeExecutionPlan(), StringComparison.Ordinal);
        Assert.True(regex.IsMatch("user@example.com"u8));
        Assert.False(regex.IsMatch("not-an-email"u8));
    }

    [Fact]
    public void MultilineAnchoredPatternUsesOwnedOperationPrograms()
    {
        var regex = new Utf8Pcre2Regex("^ERROR: .+$", options: Pcre2CompileOptions.Multiline);

        Assert.False(regex.DebugUsesUtf8RegexTranslation);
        Assert.DoesNotContain("ManagedRegex", regex.DebugDescribeExecutionPlan(), StringComparison.Ordinal);
        Assert.True(regex.IsMatch("INFO: ok\nERROR: boom"u8));
        Assert.Equal(1, regex.Count("INFO: ok\nERROR: boom"u8));
    }

    [Fact]
    public void BacktrackingShapeUsesOwnedOperationPrograms()
    {
        var regex = new Utf8Pcre2Regex(@".*(ss)");

        Assert.False(regex.DebugUsesUtf8RegexTranslation);
        Assert.True(regex.IsMatch("glass"u8));
        ReadOnlySpan<byte> match4Input = "glass"u8;
        var match4Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var match = regex.Match(match4Input, captures: match4Captures);
        Assert.True(match.Success);
        Assert.True(match4Captures[1].Success);
        Assert.Equal("ss", System.Text.Encoding.UTF8.GetString((match4Captures[1].Success ? match4Input.Slice(match4Captures[1].StartOffsetInBytes, match4Captures[1].EndOffsetInBytes - match4Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void UnicodePropertyPatternsUseTheirOwnedCharacterPrograms()
    {
        var letters = new Utf8Pcre2Regex(@"\p{L}");
        var symbols = new Utf8Pcre2Regex(@"\p{Sm}");

        Assert.False(letters.DebugUsesUtf8RegexTranslation);
        Assert.False(symbols.DebugUsesUtf8RegexTranslation);
        Assert.Equal(3, letters.Count("abc123"u8));
        Assert.Equal(1, symbols.Count("a+b"u8));
    }
}
