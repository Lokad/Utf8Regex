using System.Text;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2BoundaryLeadingSetTests
{
    private const string Date = @"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b";
    private static Utf8Pcre2ExecutionLimits Funded => new() { MatchLimit = 1000000,
        DepthLimit = 256, HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 << 20 };

    [Theory]
    [InlineData(Date)]
    [InlineData(@"\B\d+")]
    [InlineData(@"\b(?<d>\d+)\b")]
    [InlineData(@"\b\B\d+")]
    [InlineData(@"a?\b\d+")]
    public void CandidateSearchRetainsIndependentVmRangesAtEveryScalarOffset(string pattern)
    {
        var candidate = new Utf8Pcre2Regex(pattern);
        // No unnamed groups occur here; this flag changes no language/captures
        // but excludes candidate compilation under the existing request gate.
        var control = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.NoAutoCapture);
        Assert.Equal(Pcre2CandidateSearchKind.LeadingAsciiSet, candidate.DebugCompiledProgram.CandidateSearch.Kind);
        Assert.Equal(Pcre2CandidateSearchKind.None, control.DebugCompiledProgram.CandidateSearch.Kind);
        foreach (var text in new[] { "", "1/2/2024", "x11/18/2010", "11/18/201A", "a123_ 456", "a12 34",
            "é1/2/2024😀", "😀123 é456", "١٢/٣/٢٠٢٤", "_12\n34 5/6/2024!", "a12 b34", "\0_12" })
        {
            var input = Encoding.UTF8.GetBytes(text);
            for (var offset = 0;;)
            {
                foreach (var options in new[] { Pcre2MatchOptions.None, Pcre2MatchOptions.Anchored })
                {
                    Assert.Equal(control.IsMatch(input, offset, options), candidate.IsMatch(input, offset, options));
                    Assert.Equal(control.Count(input, offset, options), candidate.Count(input, offset, options));
                    EqualValues(control.Match(input, offset, options), candidate.Match(input, offset, options));
                    Assert.Equal(Ranges(control, input, offset, options), Ranges(candidate, input, offset, options));
                }
                if (offset == input.Length) break;
                Rune.DecodeFromUtf8(input.AsSpan(offset), out _, out var width); offset += width;
            }
            Assert.Equal(control.ReplaceToString(input, "<$0>"), candidate.ReplaceToString(input, "<$0>"));
        }
    }

    private static List<(int Start, int End)> Ranges(Utf8Pcre2Regex regex, byte[] input, int offset, Pcre2MatchOptions options)
    {
        var result = new List<(int, int)>();
        var cursor = regex.EnumerateMatches(input, offset, options);
        while (cursor.MoveNext()) result.Add((cursor.Current.StartOffsetInBytes, cursor.Current.EndOffsetInBytes));
        return result;
    }

    private static void EqualValues(Utf8Pcre2ValueMatch expected, Utf8Pcre2ValueMatch actual)
    {
        Assert.Equal((expected.Success, expected.StartOffsetInBytes, expected.EndOffsetInBytes),
            (actual.Success, actual.StartOffsetInBytes, actual.EndOffsetInBytes));
        if (expected.Success) Assert.Equal(expected.GetValueString(), actual.GetValueString());
    }

    [Fact]
    public void PublicDateMissUsesFewerVerifiedVmAttempts()
    {
        var input = "Today is 11/18/201A and tomorrow is 11/19/201A."u8.ToArray();
        var candidate = new Utf8Pcre2Regex(Date);
        var control = new Utf8Pcre2Regex(Date, Pcre2CompileOptions.NoAutoCapture);
        var selected = Attempts(candidate, input); var original = Attempts(control, input);
        Assert.InRange(selected, 1UL, 14UL);
        Assert.True(selected < original);

        static ulong Attempts(Utf8Pcre2Regex regex, byte[] input)
        {
            var compiled = regex.DebugCompiledProgram;
            var program = Assert.IsType<Pcre2BacktrackingDirectProgram>(compiled.Operations.Enumerate).Program;
            var subject = Utf8ValidatedInput.Create(input);
            var cursor = Pcre2GlobalMatchCursor.CreateBacktracking(program, compiled.CandidateSearchPlan,
                subject, subject.GetBytePosition(0, "startOffsetInBytes"), Pcre2MatchOptions.None,
                compiled.Request, collectDiagnostics: true);
            Assert.False(cursor.MoveNext());
            return cursor.Diagnostics.CandidateAttempts;
        }
    }

    [Theory]
    [InlineData(64, true)] [InlineData(65, false)]
    public void OptionalBoundaryProofHasAnExplicitVisitBound(int count, bool admitted)
    {
        var pattern = string.Concat(Enumerable.Repeat(@"\b", count)) + @"\d+";
        var regex = new Utf8Pcre2Regex(pattern);
        Assert.Equal(admitted ? Pcre2CandidateSearchKind.LeadingAsciiSet : Pcre2CandidateSearchKind.None,
            regex.DebugCompiledProgram.CandidateSearch.Kind);
        Assert.True(regex.IsMatch("x 123"u8));
    }

    [Theory]
    [InlineData(Pcre2CompileOptions.Ucp)] [InlineData(Pcre2CompileOptions.Caseless)]
    [InlineData(Pcre2CompileOptions.Anchored)]
    public void BroaderRequestsKeepTheExistingExclusions(Pcre2CompileOptions options)
    {
        var regex = new Utf8Pcre2Regex(Date, options);
        Assert.Equal(Pcre2CandidateSearchKind.None, regex.DebugCompiledProgram.CandidateSearch.Kind);
        var control = new Utf8Pcre2Regex(Date, options | Pcre2CompileOptions.NoAutoCapture);
        Assert.Equal(control.Count("é1/2/2024 ١/٢/٢٠٢٤"u8), regex.Count("é1/2/2024 ١/٢/٢٠٢٤"u8));
    }

    [Fact]
    public void CapturesLimitsRetainedAliasesAndStrictInputKeepTheirContracts()
    {
        const string pattern = @"\b(?<d>\d+)\b";
        var regex = new Utf8Pcre2Regex(pattern); var input = "é12 345😀"u8.ToArray();
        var retained = regex.MatchDetailed(input); var group = retained.GetGroup(1);
        var cursor = regex.EnumerateMatches(input); var alias = cursor;
        Assert.True(cursor.MoveNext()); Assert.True(alias.MoveNext());
        EqualValues(cursor.Current, alias.Current);
        while (cursor.MoveNext()) { }
        while (alias.MoveNext()) { }
        Assert.Equal("12", group.GetValueString());
        using var token = new CancellationTokenSource();
        var limited = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default, Funded, Timeout.InfiniteTimeSpan);
        Assert.Equal(regex.Count(input), limited.Count(input, 0, Pcre2MatchOptions.None, token.Token));
        var denied = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => denied.IsMatch(input)).ErrorKind);
        byte[] malformed = [.. input, 0xff];
        Assert.Throws<ArgumentException>(() => regex.IsMatch(malformed));
        token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.IsMatch(malformed, 0,
            Pcre2MatchOptions.None, token.Token)).CancellationToken);
    }
}
