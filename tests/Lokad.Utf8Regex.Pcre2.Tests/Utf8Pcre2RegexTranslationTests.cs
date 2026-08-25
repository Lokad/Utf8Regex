using System.Buffers;
using System.Text;

using global::Lokad.Utf8Regex;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Utf8Pcre2RegexTranslationTests
{
    [Fact]
    public void ExactLiteralUsesUtf8RegexTranslationForPublicSurface()
    {
        var regex = new Utf8Pcre2Regex("foo");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal("IsMatch=Pcre2Literal, Count=Pcre2Literal, Enumerate=Pcre2Literal, Match=Pcre2Literal, Replace=Pcre2Literal", regex.DebugDescribeExecutionPlan());

        Assert.True(regex.IsMatch("xxfooyy"u8));
        Assert.Equal(2, regex.Count("foo xx foo"u8));

        var match = regex.Match("xxfooyy"u8);
        Assert.True(match.Success);
        Assert.Equal(2, match.StartOffsetInBytes);
        Assert.Equal(5, match.EndOffsetInBytes);

        var enumerator = regex.EnumerateMatches("foo xx foo"u8);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current.StartOffsetInBytes);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(7, enumerator.Current.StartOffsetInBytes);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void IgnoreCaseLiteralUsesPcre2CharacterProgram()
    {
        var regex = new Utf8Pcre2Regex("httpclient", Pcre2CompileOptions.Caseless);

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal("IsMatch=Pcre2Character, Count=Pcre2Character, Enumerate=Pcre2Character, Match=Pcre2Character, Replace=Pcre2Character", regex.DebugDescribeExecutionPlan());
        Assert.Equal(3, regex.Count("HttpClient x httpclient HTTPCLIENT"u8));
    }

    [Fact]
    public void Utf8LiteralAlternationReusesCoreSearchForUnmeteredValueOperations()
    {
        var regex = new Utf8Pcre2Regex("café|naïve");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal("IsMatch=Pcre2LiteralFamily, Count=Pcre2LiteralFamily, Enumerate=Pcre2LiteralFamily, Match=Pcre2Backtracking, Replace=Pcre2Backtracking", regex.DebugDescribeExecutionPlan());

        Assert.True(regex.IsMatch("xxcafé yy naïve zz"u8));
        Assert.Equal(2, regex.Count("xxcafé yy naïve zz"u8));

        var enumerator = regex.EnumerateMatches("xxcafé yy naïve zz"u8);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("café", enumerator.Current.GetValueString());
        Assert.True(enumerator.MoveNext());
        Assert.Equal("naïve", enumerator.Current.GetValueString());
        Assert.False(enumerator.MoveNext());

        var first = regex.Match("xxcafé yy naïve zz"u8);
        Assert.True(first.Success);
        Assert.Equal("café", first.GetValueString());
    }

    [Theory]
    [InlineData("a|ab", "ab a", 2)]
    [InlineData("ab|a", "ab a", 2)]
    [InlineData("foo|foo", "foo foo", 2)]
    [InlineData("Tom|Sawyer|Huckleberry|Finn", "Tom x Finn x Sawyer", 3)]
    public void LiteralFamilyValueOperationsPreserveOrderedAlternationSemantics(
        string pattern,
        string input,
        int expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(expected, regex.Count(bytes));
        Assert.IsType<Pcre2LiteralFamilyDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
        Assert.IsType<Pcre2LiteralFamilyDirectProgram>(regex.DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2LiteralFamilyDirectProgram>(regex.DebugCompiledProgram.Operations.Enumerate);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Theory]
    [InlineData("a|ab", "a")]
    [InlineData("ab|a", "ab")]
    [InlineData("foo|foo", "foo")]
    public void LiteralFamilyEnumerationUsesBranchOrderForSameStartTies(string pattern, string expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        var enumerator = regex.EnumerateMatches("ab foo"u8);

        Assert.True(enumerator.MoveNext());
        Assert.Equal(expected, enumerator.Current.GetValueString());
    }

    [Theory]
    [InlineData("a|ab", "ab a", 0)]
    [InlineData("ab|a", "ab a", 0)]
    [InlineData("foo|foo", "foo foo", 0)]
    [InlineData("café|naïve", "é café naïve café", 3)]
    [InlineData("é|😀", "x é 😀 é", 0)]
    public void DirectPreparedLiteralFamilyRangesMatchThePublicPcre2Cursor(
        string pattern,
        string inputText,
        int startOffsetInBytes)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        var inputBytes = Encoding.UTF8.GetBytes(inputText);
        var input = Utf8ValidatedInput.Create(inputBytes);
        var start = input.GetBytePosition(startOffsetInBytes, nameof(startOffsetInBytes));
        var program = Assert.IsType<Pcre2LiteralFamilyDirectProgram>(
            regex.DebugCompiledProgram.Operations.Enumerate);
        var searcher = program.Regex.Inspection.SearchPlan.PreparedSearcher;
        Assert.Equal(PreparedSearcherKind.MultiLiteral, searcher.Kind);
        var state = new PreparedMultiLiteralScanState(start.Value, start.Value, 0);
        var preparedRanges = new List<(int Start, int End)>();
        while (searcher.TryFindNextNonOverlappingLength(
            input.Bytes,
            ref state,
            out var preparedStart,
            out var preparedLength))
        {
            preparedRanges.Add((preparedStart, preparedStart + preparedLength));
        }

        var core = program.Regex.ByteOffsetExecution.EnumeratePreparedMatches(input, start);
        var coreRanges = new List<(int Start, int End)>();
        while (core.MoveNext())
        {
            coreRanges.Add((core.StartOffsetInBytes, core.EndOffsetInBytes));
        }

        var current = regex.EnumerateMatches(inputBytes, startOffsetInBytes);
        var currentRanges = new List<(int Start, int End)>();
        while (current.MoveNext())
        {
            currentRanges.Add((current.Current.StartOffsetInBytes, current.Current.EndOffsetInBytes));
        }

        Assert.Equal(preparedRanges, coreRanges);
        Assert.Equal(currentRanges, preparedRanges);
    }

    [Theory]
    [InlineData("a|ab", "ab a", "ExactDirectFamily", 2)]
    [InlineData("alpha|amber|bravo", "alpha amber bravo", "ExactTrieFamily", 3)]
    [InlineData("Sherlock Holmes|John Watson|Irene Adler|Inspector Lestrade|Professor Moriarty", "Sherlock Holmes x Professor Moriarty", "ExactAutomatonFamily", 2)]
    [InlineData("夏洛克·福尔摩斯|约翰华生|阿德勒|雷斯垂德|莫里亚蒂教授", "夏洛克·福尔摩斯 x 莫里亚蒂教授", "ExactPackedFamily", 2)]
    [InlineData("Task|ValueTask|IAsyncEnumerable", "Task ValueTask IAsyncEnumerable", "ExactEarliestFamily", 3)]
    public void PreparedLiteralFamilyCursorCoversEveryCorePortfolio(
        string pattern,
        string input,
        string expectedPortfolio,
        int expectedCount)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        var program = Assert.IsType<Pcre2LiteralFamilyDirectProgram>(
            regex.DebugCompiledProgram.Operations.Enumerate);

        Assert.Equal(expectedPortfolio, program.Regex.ByteOffsetExecution.SearchPortfolioKind.ToString());
        var enumerator = regex.EnumerateMatches(Encoding.UTF8.GetBytes(input));
        var count = 0;
        while (enumerator.MoveNext())
        {
            Assert.True(enumerator.Current.HasContiguousByteRange);
            Assert.True(enumerator.Current.IsUtf8SliceWellFormed);
            Assert.True(enumerator.Current.HasUtf16Projection);
            count++;
        }

        Assert.Equal(expectedCount, count);
    }

    [Fact]
    public void LiteralFamilyCursorFeedsMatchManyAndEveryReplacementShape()
    {
        var regex = new Utf8Pcre2Regex("tempus|magna|semper");
        var input = "é tempus magna semper tempus"u8;

        Assert.Equal(4, regex.Count(input));
        var enumerator = regex.EnumerateMatches(input);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(3, enumerator.Current.StartOffsetInBytes);
        Assert.Equal(2, enumerator.Current.StartOffsetInUtf16);

        Span<Utf8Pcre2MatchData> matches = stackalloc Utf8Pcre2MatchData[3];
        Assert.Equal(3, regex.MatchMany(input, matches, out var isMore));
        Assert.True(isMore);
        Assert.Equal(16, matches[2].StartOffsetInBytes);

        const string expected = "é <tempus> <magna> <semper> <tempus>";
        Assert.Equal(expected, regex.ReplaceToString(input, "<$0>"));
        Assert.Equal(expected, Encoding.UTF8.GetString(regex.Replace(input, "<$0>"u8)));
        Assert.Equal(
            expected,
            regex.ReplaceToString(
                input,
                0,
                static (in Utf8Pcre2MatchContext match, ref int _) => $"<{match.GetValueString()}>"));

        var evaluatorBytes = regex.Replace(
            input,
            0,
            static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref int _) =>
            {
                writer.AppendAsciiByte((byte)'<');
                writer.Append(match.Value.GetValueBytes());
                writer.AppendAsciiByte((byte)'>');
            });
        Assert.Equal(expected, Encoding.UTF8.GetString(evaluatorBytes));

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
                Pcre2SubstitutionOptions.SubstituteOverflowLength));
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
        Assert.Equal("foo", anchoredMatch.Current.GetValueString());
        Assert.False(anchoredMatch.MoveNext());
    }

    [Fact]
    public void MeteredLiteralFamilyCountRetainsPcre2ResourceSemantics()
    {
        var regex = new Utf8Pcre2Regex(
            "foo|bar",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1 },
            Timeout.InfiniteTimeSpan);

        var exception = Assert.Throws<Pcre2MatchException>(() => regex.Count("xxfoo"u8));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, exception.ErrorKind);
    }

    [Fact]
    public void LiteralFamilyCountStillValidatesTheEntireUtf8Subject()
    {
        var regex = new Utf8Pcre2Regex("foo|bar");
        var malformedAfter = new byte[] { (byte)'f', (byte)'o', (byte)'o', 0xC3, 0x28 };
        var malformedBefore = new byte[] { 0xC3, 0x28, (byte)'f', (byte)'o', (byte)'o' };

        Assert.Throws<ArgumentException>(() => regex.Count(malformedAfter));
        Assert.Throws<ArgumentException>(() => regex.Count(malformedBefore));
    }

    [Fact]
    public void LiteralFamilyCountLeavesEmptyAlternativesAndCompileAnchorsOnBacktracking()
    {
        var empty = new Utf8Pcre2Regex("foo|");
        var anchored = new Utf8Pcre2Regex("foo|bar", Pcre2CompileOptions.Anchored);

        Assert.IsType<Pcre2BacktrackingDirectProgram>(empty.DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(anchored.DebugCompiledProgram.Operations.Count);
        Assert.Equal(3, empty.Count("xfoo"u8));
        Assert.Equal(0, anchored.Count("xfoo"u8));
    }

    [Fact]
    public void OptionalBarPatternUsesLiteralFamilyForCaptureIndependentOperations()
    {
        var regex = new Utf8Pcre2Regex("foo(?<Bar>BAR)?");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.False(regex.DebugHasManagedRegex);
        Assert.Equal("IsMatch=Pcre2LiteralFamily, Count=Pcre2LiteralFamily, Enumerate=Pcre2LiteralFamily, Match=Pcre2Backtracking, Replace=Pcre2Backtracking", regex.DebugDescribeExecutionPlan());

        var match = regex.Match("xxfooBARzz"u8);
        Assert.True(match.Success);
        Assert.Equal("fooBAR", match.GetValueString());

        Assert.Equal(2, regex.Count("foo fooBAR x"u8));
    }

    [Fact]
    public void OptionalBarGenericTemplateReplacementUsesNamedCaptureWithoutManagedRegex()
    {
        var regex = new Utf8Pcre2Regex("foo(?<Bar>BAR)?");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.False(regex.DebugHasManagedRegex);

        var replaced = regex.Replace("fooBAR x"u8, "<$0|$Bar>");

        Assert.Equal("<fooBAR|BAR> x", Encoding.UTF8.GetString(replaced));
    }

    [Fact]
    public void OptionalBarGenericEvaluatorReplacementUsesDetailedCapturesWithoutManagedRegex()
    {
        var regex = new Utf8Pcre2Regex("foo(?<Bar>BAR)?");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.False(regex.DebugHasManagedRegex);

        var state = 0;
        var replaced = regex.ReplaceToString(
            "foo fooBAR x"u8,
            state,
            static (in Utf8Pcre2MatchContext match, ref int _) =>
            {
                var bar = match.TryGetFirstSetGroup("Bar", out var named) && named.Success
                    ? named.GetValueString()
                    : "none";
                return $"<{match.Value.GetValueString()}|{bar}>";
            });

        Assert.Equal("<foo|none> <fooBAR|BAR> x", replaced);
    }

    [Fact]
    public void AbPlusPatternUsesDirectCaptureIndependentGlobalsAndVmForDetailedSurface()
    {
        var regex = new Utf8Pcre2Regex("(a)b+");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal("IsMatch=Pcre2LiteralPrefixRepeat, Count=Pcre2LiteralPrefixRepeat, Enumerate=Pcre2LiteralPrefixRepeat, Match=Pcre2Backtracking, Replace=Pcre2Backtracking", regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch("xxabbbzz"u8));
        Assert.Equal(2, regex.Count("ab abb"u8));

        var probe = regex.Probe("a"u8, Pcre2PartialMode.Hard);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, probe.Kind);
        Assert.Equal("a", probe.GetPartial().Value.GetValueString());
    }

    [Fact]
    public void AsciiQuantifiedPatternUsesBoundedIsMatchAndVmForGlobalOperations()
    {
        var regex = new Utf8Pcre2Regex(@"\s[a-zA-Z]{0,12}ing\s");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal("IsMatch=Pcre2AsciiBoundedIsMatch, Count=Pcre2Backtracking, Enumerate=Pcre2Backtracking, Match=Pcre2Backtracking, Replace=Pcre2Backtracking", regex.DebugDescribeExecutionPlan());
        Assert.Equal(3, regex.Count(" sing  bringing  going  go "u8));
    }

    [Fact]
    public void OrderedWindowUsesPreparedCountAndBacktrackingForOtherOperations()
    {
        var regex = new Utf8Pcre2Regex("Tom.{10,25}river|river.{10,25}Tom");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal("IsMatch=Pcre2Backtracking, Count=Pcre2AsciiOrderedLiteralWindow, Enumerate=Pcre2Backtracking, Match=Pcre2Backtracking, Replace=Pcre2Backtracking", regex.DebugDescribeExecutionPlan());
        Assert.Equal(Pcre2CandidateSearchKind.BranchLeadingLiterals, regex.DebugCompiledProgram.CandidateSearch.Kind);
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
        Assert.Equal("abcdefghij", regex.Match(input).GetValueString());

        Span<Utf8Pcre2MatchData> matches = stackalloc Utf8Pcre2MatchData[3];
        Assert.Equal(3, regex.MatchMany(input, matches, out var isMore));
        Assert.True(isMore);
        Assert.Equal(
            "short <abcdefghij> <x_123456789> <yyyyyyyyyy> é<abcdefghij>",
            regex.ReplaceToString(input, "<$0>"));

        var diagnostics = regex.DebugCountWithDiagnostics(input, 0);
        Assert.Equal(4, diagnostics.Count);
        Assert.Equal(4UL, diagnostics.Execution.CandidateAttempts);
    }

    [Theory]
    [InlineData(@"\B\w{10,}\b", Pcre2CompileOptions.None)]
    [InlineData(@"\b\w+\b", Pcre2CompileOptions.None)]
    [InlineData(@"(\b\w{10,}\b)", Pcre2CompileOptions.None)]
    [InlineData(@"\b\w{10,}\b", Pcre2CompileOptions.Ucp)]
    [InlineData(@"\b\w{10,}\b", Pcre2CompileOptions.Caseless)]
    public void LeadingAsciiWordBoundaryRunSearchRejectsBroaderSemantics(
        string pattern,
        Pcre2CompileOptions options)
    {
        var regex = new Utf8Pcre2Regex(pattern, options);

        Assert.NotEqual(Pcre2CandidateSearchKind.LeadingAsciiWordBoundaryRun, regex.DebugCompiledProgram.CandidateSearch.Kind);
    }

    [Fact]
    public void LeadingAsciiWordBoundaryRunSearchFallsBackForRuntimeLimitsAndOptions()
    {
        var input = "abcdefghij klmnopqrst"u8.ToArray();
        var regex = new Utf8Pcre2Regex(@"\b\w{10,}\b");
        Assert.Equal(2, regex.Count(input));
        Assert.Equal(1, regex.Count(input, 2));
        Assert.True(regex.IsMatch(input, 0, Pcre2MatchOptions.Anchored));
        Assert.False(regex.IsMatch(input, 2, Pcre2MatchOptions.Anchored));

        var limited = new Utf8Pcre2Regex(
            @"\b\w{10,}\b",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1 },
            Timeout.InfiniteTimeSpan);
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => limited.Count(input)).ErrorKind);
    }

    [Fact]
    public void LeadingAsciiWordBoundaryRunSearchSupportsConcurrentRegexReuse()
    {
        var regex = new Utf8Pcre2Regex(@"\b\w{10,}\b");
        var input = "short abcdefghij klmnopqrst"u8.ToArray();

        Parallel.For(0, 256, _ =>
        {
            Assert.Equal(2, regex.Count(input));
            Assert.Equal("short <abcdefghij> <klmnopqrst>", regex.ReplaceToString(input, "<$0>"));
        });
    }

    [Fact]
    public void LeadingAsciiWordBoundaryRunSearchMatchesVmOnlyGeneratedCases()
    {
        string[] patterns =
        [
            @"\b\w{2,}\b",
            @"\b\w{3,5}\b",
            @"\b\w{4,}?\b",
            @"\b\w{4,}+\b",
            @"\b\w{3,}\B",
        ];
        string[] fragments = ["a", "Z", "0", "_", " ", "-", ".", "\n", "é", "😀"];
        var random = new Random(0x5EED);
        foreach (var pattern in patterns)
        {
            var candidate = new Utf8Pcre2Regex(pattern);
            var vmOnly = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.NoAutoCapture);
            Assert.Equal(Pcre2CandidateSearchKind.LeadingAsciiWordBoundaryRun, candidate.DebugCompiledProgram.CandidateSearch.Kind);
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
                Assert.Equal(vmOnly.ReplaceToString(input, "<$0>"), candidate.ReplaceToString(input, "<$0>"));
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
    [InlineData(@"(foo)\1")]
    [InlineData(@"(?R)|foo")]
    public void UnsafeStartSemanticsStayOffTheCandidateSearchPath(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        Assert.Equal(Pcre2CandidateSearchKind.None, regex.DebugCompiledProgram.CandidateSearch.Kind);
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
        Assert.Equal("bc.d", enumerator.Current.GetValueString());
        Assert.True(enumerator.MoveNext());
        Assert.Equal("bc.e", enumerator.Current.GetValueString());
        Assert.True(enumerator.MoveNext());
        Assert.Equal("β.γ", enumerator.Current.GetValueString());
        Assert.False(enumerator.MoveNext());

        Span<Utf8Pcre2MatchData> destination = stackalloc Utf8Pcre2MatchData[3];
        Assert.Equal(3, regex.MatchMany(input, destination, out var isMore));
        Assert.False(isMore);
        Assert.Equal("<bc.d>za<bc.e>za<β.γ>z", Encoding.UTF8.GetString(regex.Replace(input, "<$0>")));
    }

    [Fact]
    public void OverlappingLeadingRunCandidateSearchHonorsStartAndMetering()
    {
        var regex = new Utf8Pcre2Regex(@"[^a]+\.[^z]+");
        var input = "xbc.de"u8;

        var match = regex.Match(input, 2);
        Assert.True(match.Success);
        Assert.Equal("c.de", match.GetValueString());

        var metered = new Utf8Pcre2Regex(
            @"[^a]+\.[^z]+",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1 },
            Timeout.InfiniteTimeSpan);
        Assert.Throws<Pcre2MatchException>(() => metered.IsMatch("no delimiter"u8));
    }

    [Fact]
    public void LeadingRunCandidateSearchRespectsAStartInsideTheRun()
    {
        var regex = new Utf8Pcre2Regex(@"[a-z]+@[a-z]+");
        var input = "foo@bar"u8;

        var match = regex.Match(input, 1);

        Assert.True(match.Success);
        Assert.Equal(1, match.StartOffsetInBytes);
        Assert.Equal("oo@bar", match.GetValueString());
    }

    [Theory]
    [InlineData("Tom.{10,25}river|river.{10,25}Tom")]
    [InlineData(@"[a-z]+@[a-z]+")]
    [InlineData(@"[^a]+\.[^z]+")]
    [InlineData(@"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])")]
    [InlineData(@"\b\w{10,}\b")]
    public void CandidateSearchStillValidatesMalformedPrefixesAndSuffixes(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        var malformedPrefix = new byte[] { 0xC3, 0x28, (byte)'1', (byte)'9', (byte)'2', (byte)'.', (byte)'1' };
        var malformedSuffix = new byte[] { (byte)'f', (byte)'o', (byte)'o', (byte)'@', (byte)'b', (byte)'a', (byte)'r', 0xC3, 0x28 };

        Assert.NotEqual(Pcre2CandidateSearchKind.None, regex.DebugCompiledProgram.CandidateSearch.Kind);
        Assert.Throws<ArgumentException>(() => regex.Count(malformedPrefix));
        Assert.Throws<ArgumentException>(() => regex.Count(malformedSuffix));
    }

    [Fact]
    public void ConfiguredLimitsBypassCandidateSearchAndRetainPcre2Accounting()
    {
        var regex = new Utf8Pcre2Regex(
            @"[a-z]+@[a-z]+",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1 },
            Timeout.InfiniteTimeSpan);

        var exception = Assert.Throws<Pcre2MatchException>(() => regex.Count("xx foo@bar"u8));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, exception.ErrorKind);
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

        var match = regex.Match(input, 2);
        Assert.True(match.Success);
        Assert.Equal(" bringing ", match.GetValueString());
    }

    [Fact]
    public void BoundedRequiredLiteralWindowSupportsWideAsciiPrefixes()
    {
        var regex = new Utf8Pcre2Regex(@"[\x00-\x7F]{1,2}foo");

        Assert.Equal(Pcre2CandidateSearchKind.BoundedLiteralWindow, regex.DebugCompiledProgram.CandidateSearch.Kind);
        Assert.Equal(2, regex.Count("xfoo yyfoo z"u8));
    }

    [Fact]
    public void DateValidatorUsesBoundedIsMatchAndVmForDetailedOperations()
    {
        var regex = new Utf8Pcre2Regex(@"^\d{1,2}/\d{1,2}/\d{4}$");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal("IsMatch=Pcre2AsciiBoundedIsMatch, Count=Pcre2Backtracking, Enumerate=Pcre2Backtracking, Match=Pcre2Backtracking, Replace=Pcre2Backtracking", regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch("12/12/2001"u8));
        Assert.False(regex.IsMatch("12-12-2001"u8));
    }

    [Fact]
    public void CreditCardValidatorUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"(\d{4}[- ]){3}\d{3,4}");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal("IsMatch=Pcre2AsciiBoundedIsMatch, Count=Pcre2Backtracking, Enumerate=Pcre2Backtracking, Match=Pcre2Backtracking, Replace=Pcre2Backtracking", regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch("1234-5678-1234-456"u8));
        Assert.False(regex.IsMatch("1234-5678"u8));
    }

    [Fact]
    public void FloatValidatorUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"^[-+]?\d*\.?\d*$");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.True(regex.IsMatch("-3.14159"u8));
        Assert.False(regex.IsMatch("3.14.159"u8));
    }

    [Fact]
    public void DateSearchUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.True(regex.IsMatch("today is 12/12/2001 indeed"u8));
        Assert.False(regex.IsMatch("today is 12-12-2001 indeed"u8));
    }

    [Fact]
    public void IpSearchUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.True(regex.IsMatch("127.000.000.001"u8));
        Assert.False(regex.IsMatch("not an ip address"u8));
    }

    [Fact]
    public void UriSearchUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.True(regex.IsMatch("http://example.com/path?q=1#frag"u8));
        Assert.False(regex.IsMatch("not a uri"u8));
    }

    [Fact]
    public void TranslatedGroupZeroOnlyPatternUsesUtf8RegexForDetailedMatch()
    {
        var regex = new Utf8Pcre2Regex(@"\w{10,}");

        var match = regex.MatchDetailed("abcdefghij short"u8);

        Assert.True(match.Success);
        Assert.Equal("abcdefghij", match.Value.GetValueString());
        Assert.Equal(0, match.Value.StartOffsetInBytes);
        Assert.Equal(10, match.Value.EndOffsetInBytes);
    }

    [Fact]
    public void TranslatedCapturedPatternUsesUtf8RegexForDetailedMatch()
    {
        var regex = new Utf8Pcre2Regex("foo(?<Bar>BAR)?");

        var match = regex.MatchDetailed("xxfooBARzz"u8, startOffsetInBytes: 2);

        Assert.True(match.Success);
        Assert.Equal("fooBAR", match.Value.GetValueString());

        var whole = match.GetGroup(0);
        Assert.True(whole.Success);
        Assert.Equal("fooBAR", whole.GetValueString());

        var capture = match.GetGroup(1);
        Assert.True(capture.Success);
        Assert.Equal("BAR", capture.GetValueString());

        Assert.True(match.TryGetFirstSetGroup("Bar", out var named));
        Assert.True(named.Success);
        Assert.Equal("BAR", named.GetValueString());
    }

    [Fact]
    public void LongWordCountUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"\w{10,}");
        var input = "abcdefghij short klmnopqrst"u8;

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        var direct = Assert.IsType<Pcre2SingleTokenRepeatDirectProgram>(regex.DebugCompiledProgram.Operations.Count);
        Assert.Equal(10, direct.Program.Minimum);
        Assert.Equal(int.MaxValue, direct.Program.Maximum);
        Assert.Null(direct.Program.LeadingAsciiByte);
        Assert.Null(direct.Program.GreedyExcludedAsciiByte);
        Assert.Equal(Pcre2CompileOptions.None, direct.Program.Request.Options & Pcre2CompileOptions.Anchored);
        Assert.True(regex.Match(input, 17).Success);
        Assert.True(regex.Match(input, 16).Success);
        Assert.True(regex.Match(input, 10).Success);
        Assert.Equal(2, regex.Count(input));
    }

    [Fact]
    public void BoundaryLongWordCountUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"\b\w{10,}\b");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal(2, regex.Count("abcdefghij short klmnopqrst"u8));
    }

    [Fact]
    public void HolmesWordCountUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"\w+\s+Holmes");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal(2, regex.Count("Mr Holmes and Sherlock Holmes"u8));
    }

    [Fact]
    public void NonNewlineCountUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"[^\n]*");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        var direct = Assert.IsType<Pcre2SingleTokenRepeatDirectProgram>(regex.DebugCompiledProgram.Operations.Count);
        Assert.Equal((byte)'\n', direct.Program.GreedyExcludedAsciiByte);
        Assert.Equal(4, regex.Count("abc\ndef"u8));
    }

    [Fact]
    public void FtpLineValidatorUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"^([0-9]+)(\-| |$)(.*)$");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.True(regex.IsMatch("100- this is a line of ftp response"u8));
        Assert.False(regex.IsMatch("ftp response without code"u8));
    }

    [Fact]
    public void EmailPatternUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.False(regex.DebugHasManagedRegex);
        Assert.True(regex.IsMatch("user@example.com"u8));
        Assert.False(regex.IsMatch("not-an-email"u8));
    }

    [Fact]
    public void MultilineAnchoredPatternUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex("^ERROR: .+$", Pcre2CompileOptions.Multiline);

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.False(regex.DebugHasManagedRegex);
        Assert.True(regex.IsMatch("INFO: ok\nERROR: boom"u8));
        Assert.Equal(1, regex.Count("INFO: ok\nERROR: boom"u8));
    }

    [Fact]
    public void BacktrackingShapeUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@".*(ss)");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.True(regex.IsMatch("glass"u8));
        var match = regex.MatchDetailed("glass"u8);
        Assert.True(match.Success);
        Assert.True(match.GetGroup(1).Success);
        Assert.Equal("ss", match.GetGroup(1).GetValueString());
    }

    [Fact]
    public void UnicodePropertyPatternsUseUtf8RegexTranslation()
    {
        var letters = new Utf8Pcre2Regex(@"\p{L}");
        var symbols = new Utf8Pcre2Regex(@"\p{Sm}");

        Assert.True(letters.DebugUsesUtf8RegexTranslation);
        Assert.True(symbols.DebugUsesUtf8RegexTranslation);
        Assert.Equal(3, letters.Count("abc123"u8));
        Assert.Equal(1, symbols.Count("a+b"u8));
    }
}
