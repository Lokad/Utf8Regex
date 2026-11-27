using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2LiteralCountBridgeTests
{
    [Fact]
    public void FoldedCountKeepsByteOffsetsNonoverlapAndMatchFlags()
    {
        using var regex = new Utf8Pcre2Regex("Aa", Pcre2CompileOptions.Caseless);
        using var token = new CancellationTokenSource();
        Assert.Equal(3, regex.Count("😀aAaAa\nAa"u8, 4));
        Assert.Equal(2, regex.Count("😀aAaAa\nAa"u8, 6, cancellationToken: token.Token));
        Assert.Equal(0, regex.Count("😀aAaAa\nAa"u8, 12));
        Assert.Equal(0, regex.Count([]));
        Assert.Equal(2, regex.Count("😀aAaAa\nAa"u8, 4, Pcre2MatchOptions.Anchored));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count("😀ab"u8, 1));
    }

    [Theory]
    [InlineData(Pcre2MatchOptions.None, 4, 4, 7, 10)]
    [InlineData(Pcre2MatchOptions.Anchored, 4, 4)]
    [InlineData(Pcre2MatchOptions.EndAnchored, 4, 10)]
    [InlineData(Pcre2MatchOptions.Anchored | Pcre2MatchOptions.EndAnchored, 10, 10)]
    [InlineData(Pcre2MatchOptions.Anchored | Pcre2MatchOptions.EndAnchored, 4)]
    public void FoldedEnumerationKeepsByteOffsetsAndMatchFlags(Pcre2MatchOptions flags, int start,
        params int[] expected)
    {
        using var regex = new Utf8Pcre2Regex("Aa", Pcre2CompileOptions.Caseless);
        using var token = new CancellationTokenSource();
        var cursor = regex.EnumerateMatches("😀aA aaXAA"u8, start, flags, token.Token);
        foreach (var offset in expected)
        {
            Assert.True(cursor.MoveNext());
            Assert.Equal(offset, cursor.Current.StartOffsetInBytes);
            Assert.Equal(offset + 2, cursor.Current.EndOffsetInBytes);
        }
        Assert.False(cursor.MoveNext());
        Assert.Equal(expected.Length, regex.Count("😀aA aaXAA"u8, start, flags, token.Token));
    }

    [Theory]
    [InlineData("ki", "Ki Ki kİ", 2, false)]
    [InlineData("si", "ſI Si sı", 2, false)]
    [InlineData("ii", "İi Ii ıi", 1, true)]
    [InlineData("(?-i:a)B", "AB aB ab", 2, false)]
    public void FoldedCountRequiresTheCompletePcre2CaseFiber(string pattern, string text, int expected, bool prepared)
    {
        using var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.Caseless);
        Assert.Equal(prepared, regex.DebugCompiledProgram.Operations.Count is Pcre2LiteralDirectProgram);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.Count(input));
        var found = 0;
        var cursor = regex.EnumerateMatches(input);
        while (cursor.MoveNext()) found++;
        Assert.Equal(expected, found);
    }

    [Theory]
    [InlineData("(a)(b)", "π😀ab abxab", 6, 3)]
    [InlineData("(cat|horse|dog)", "π😀horse catdog", 6, 3)]
    [InlineData("(a|aa)", "aaaa", 0, 4)]
    [InlineData("(aa|a)", "aaaa", 0, 2)]
    [InlineData(@"(?>a\Kba|ab)", "π😀abab", 6, 1)]
    [InlineData(@"(?>ab|a\Kba)", "π😀abab", 6, 2)]
    [InlineData("(😀)(é)", "π😀é x😀é", 2, 2)]
    [InlineData("(a)(b)", "π😀xx", 2, 0)]
    public void FiniteCountKeepsOrderedScalarsAndBorrowedOffsets(string pattern, string text, int start, int expected)
    {
        using var ordinary = new Utf8Pcre2Regex(pattern);
        var regex = Create(pattern);
        Assert.IsType<Pcre2FiniteLiteralLanguageDirectProgram>(regex.DebugCompiledProgram.Operations.Count);
        var storage = Encoding.UTF8.GetBytes("pad" + text + "pad");
        var input = storage.AsSpan(3, Encoding.UTF8.GetByteCount(text));
        using var token = new CancellationTokenSource();
        Assert.Equal(expected, ordinary.Count(input, start));
        Assert.Equal(expected, regex.Count(input, start));
        Assert.Equal(expected, regex.Count(input, start, Pcre2MatchOptions.None, token.Token));
    }

    [Fact]
    public void PureCountUsesItsSmallInvocationWithoutVmScratch()
    {
        var regex = Create("(a)(b)", new()
        {
            DepthLimit = 1,
            HeapLimitInBytes = 1,
            ResultLimitInBytes = 1
        });
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("ab ", 1000)));
        Assert.Equal(1000, regex.Count(bytes));
        Assert.Equal(Pcre2ErrorKind.HeapLimit,
            Assert.Throws<Pcre2MatchException>(() =>
            {
                ReadOnlyMemory<byte> result1Input = bytes;
                using var result1Cursor = regex.CreateMatchCursor(result1Input);
                result1Cursor.MoveNext(out var result1Result);
                result1Cursor.Dispose();
                _ = result1Result;
            }).ErrorKind);
    }

    [Fact]
    public void ExplicitOptionsAndByteOffsetsPreserveRetainedCaptures()
    {
        var regex = Create("(a)(b)");
        ReadOnlySpan<byte> input = "😀ab ab"u8;
        var captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        Assert.True(regex.Match(input, startOffsetInBytes: 4, captures: captures).Success);
        Assert.Equal(1, regex.Count("😀ab ab"u8, 4, Pcre2MatchOptions.Anchored));
        Assert.Equal(2, regex.Count("😀ab ab"u8, 4));
        Assert.Equal((4, 5), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
        Assert.Equal((5, 6), (captures[2].StartOffsetInBytes, captures[2].EndOffsetInBytes));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count("😀ab"u8, 1));
    }

    [Fact]
    public void CountPreservesBorrowedParentCapacity()
    {
        var regex = Create("(a)(b)", new() { DepthLimit = 1, HeapLimitInBytes = 1048576, ResultLimitInBytes = 1 });
        var compiled = regex.DebugCompiledProgram;
        var invocation = Pcre2SliceDriver.CreateInvocation(compiled.Request, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(13));
        try
        {
            Pcre2Runner.AdmitStart("ab ab ab ab"u8, 0, ref invocation);
            Assert.True(Pcre2GlobalOperationDriver.TryCount(compiled, "ab ab ab ab"u8,
                new Utf8BytePosition(0), Pcre2MatchOptions.None, ref invocation, out var count));
            Assert.Equal(4, count);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
        finally { invocation.Ledger.Release(13); }
    }

    private static Utf8Pcre2Regex Create(string pattern, Utf8Pcre2ExecutionLimits? limits = null,
        Pcre2CompileOptions options = Pcre2CompileOptions.None)
    {
        var selected = limits ?? new Utf8Pcre2ExecutionLimits
        {
            DepthLimit = 1024,
            HeapLimitInBytes = 1048576,
            ResultLimitInBytes = 1048576
        };
        return new Utf8Pcre2Regex(pattern, options, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = selected.ToSliceLimits().ToResourcePolicy(),
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
    }
}
