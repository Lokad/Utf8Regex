using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ResultPolicyTests
{
    private static Utf8Pcre2Regex Create(string pattern, ulong resultBytes) => new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
    {
        ResourcePolicy = new()
        {
            MaxResultBytes = resultBytes
        },
        MatchTimeout = Timeout.InfiniteTimeSpan
    });

    [Theory]
    [InlineData("A", "A")]
    [InlineData("[A-Z]", "A")]
    [InlineData("(é)(😀)", "é😀")]
    [InlineData("(*MARK:x)(é)(😀)", "é😀")]
    [InlineData(@"\x41+", "AAA")]
    public void ResultOnlyPolicyBoundsCursorPublicationWithoutBoundingScalarValues(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var tiny = Create(pattern, 1);
        Assert.True(tiny.IsMatch(input));
        Assert.Equal(1, tiny.Count(input));
        Assert.True(tiny.Match(input).Success);
        var error = Assert.Throws<Pcre2MatchException>(() =>
        {
            ReadOnlyMemory<byte> result1Input = input;
            using var result1Cursor = tiny.CreateMatchCursor(result1Input);
            result1Cursor.MoveNext(out var result1Result);
            result1Cursor.Dispose();
            _ = result1Result;
        });
        Assert.Equal("ResultLimit", error.ErrorKind.ToString());
        var result2Regex = Create(pattern, 1 << 20);
        ReadOnlyMemory<byte> result2Input = input;
        using var result2Cursor = result2Regex.CreateMatchCursor(result2Input);
        result2Cursor.MoveNext(out var result2Result);
        result2Cursor.Dispose();
        Assert.Equal(text, System.Text.Encoding.UTF8.GetString((result2Result.Value.Success ? result2Input.Span.Slice(result2Result.Value.StartOffsetInBytes, result2Result.Value.EndOffsetInBytes - result2Result.Value.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnedNoMatchOutputCannotEscapeResultAdmission(bool text)
    {
        var regex = Create("A", 1);
        var error = Assert.Throws<Pcre2MatchException>(() =>
        {
            if (text) _ = System.Text.Encoding.UTF8.GetString(regex.Replace("zzz"u8, "!"u8));
            else _ = regex.Replace("zzz"u8, "!"u8);
        });
        Assert.Equal("ResultLimit", error.ErrorKind.ToString());
    }

    [Fact]
    public void ResultCapacityIsCumulativeWithoutInvalidatingPublishedSnapshots()
    {
        var bytes = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(1);
        var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, 0, 2 * bytes - 1), Timeout.InfiniteTimeSpan, default);
        var input = Utf8ValidatedInput.BorrowBytes("😀"u8);
        var first = Pcre2Runner.PublishSliceCaptures(1, 0, 4, [], [], ref input, ref invocation);
        Pcre2SliceDriver.DetachProjectedGroups(first, ref invocation);
        var alias = invocation;
        Exception? failure = null;
        try { _ = Pcre2Runner.PublishSliceCaptures(1, 0, 4, [], [], ref input, ref alias); }
        catch (Exception error) { failure = error; }
        Assert.Equal("ResultLimit", Assert.IsType<Pcre2MatchException>(failure).ErrorKind.ToString());
        Assert.Equal(Utf8ExecutionLimitKind.ResultBytes, invocation.TryChargeStep());
        Assert.Equal((0, 4), (first[0].StartOffsetInBytes, first[0].EndOffsetInBytes));
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData("A", "A", 1)]
    [InlineData("(é)(😀)", "é😀", 3)]
    [InlineData(@"(?<v>\x41)(😀)", "A😀", 3)]
    public void CursorCaptureArrayHasAnExactModeledBoundary(string pattern, string text, int slots)
    {
        var cap = Utf8ConstructionBudget.ArrayBytes<Utf8Pcre2CaptureRange>(slots);
        var input = Encoding.UTF8.GetBytes(text);
        var result3Regex = Create(pattern, cap);
        ReadOnlyMemory<byte> result3Input = input;
        using var result3Cursor = result3Regex.CreateMatchCursor(result3Input);
        result3Cursor.MoveNext(out var result3Result);
        result3Cursor.Dispose();
        Assert.Equal(text, System.Text.Encoding.UTF8.GetString((result3Result.Value.Success ? result3Input.Span.Slice(result3Result.Value.StartOffsetInBytes, result3Result.Value.EndOffsetInBytes - result3Result.Value.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("ResultLimit", Assert.Throws<Pcre2MatchException>(() =>
        {
            var result4Regex = Create(pattern, cap - 1);
            ReadOnlyMemory<byte> result4Input = input;
            using var result4Cursor = result4Regex.CreateMatchCursor(result4Input);
            result4Cursor.MoveNext(out var result4Result);
            result4Cursor.Dispose();
            _ = result4Result;
        }).ErrorKind.ToString());
        var result5Regex = Create(pattern, cap);
        ReadOnlyMemory<byte> result5Input = input;
        using var result5Cursor = result5Regex.CreateMatchCursor(result5Input);
        result5Cursor.MoveNext(out var result5Result);
        result5Cursor.Dispose();
        Assert.Equal(text, System.Text.Encoding.UTF8.GetString((result5Result.Value.Success ? result5Input.Span.Slice(result5Result.Value.StartOffsetInBytes, result5Result.Value.EndOffsetInBytes - result5Result.Value.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Theory]
    [InlineData("A", "A")]
    [InlineData("(a)b+", "abbb")]
    public void FullProbeChargesOwnedCapturesWhileHardPartialRemainsInline(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal("ResultLimit", Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(pattern, 1).Probe(input, Pcre2PartialMode.Soft); }).ErrorKind.ToString());
        if (pattern == "(a)b+") Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, Create(pattern, 1).Probe(input, Pcre2PartialMode.Hard).Kind);
        Assert.Equal(text, System.Text.Encoding.UTF8.GetString(Create(pattern, 4096).Probe(input, Pcre2PartialMode.Soft).GetMatch().Value.GetValueBytes()));
    }

    [Theory]
    [InlineData("A", "AzA", "!", "!z!", Pcre2SubstitutionOptions.None)]
    [InlineData(@"(?<v>\x41)😀", "A😀xA😀", "${v}!", "A!xA!", Pcre2SubstitutionOptions.None)]
    [InlineData("(*MARK:x)(é)(😀)", "é😀xé😀", "$2$1", "😀éx😀é", Pcre2SubstitutionOptions.None)]
    [InlineData("A", "AzA", @"\Ué\E$0", "ÉAzÉA", Pcre2SubstitutionOptions.Extended)]
    public void ActualReplacementProducersEnforceCumulativeResultCapacity(string pattern, string text,
        string replacement, string expected, Pcre2SubstitutionOptions options)
    {
        var input = Encoding.UTF8.GetBytes(text);
        string Invoke(ulong cap) => System.Text.Encoding.UTF8.GetString(Create(pattern, cap).Replace(input, Encoding.UTF8.GetBytes(replacement), 0, options));
        Assert.Equal(expected, Invoke(1 << 20));
        ulong first = 1, last = 1 << 20;
        while (first < last)
        {
            var cap = first + (last - first) / 2;
            try { Assert.Equal(expected, Invoke(cap)); last = cap; }
            catch (Pcre2MatchException error) { Assert.Equal("ResultLimit", error.ErrorKind.ToString()); first = cap + 1; }
        }
        Assert.InRange(first, 2UL, (1UL << 20) - 1);
        Assert.Equal("ResultLimit", Assert.Throws<Pcre2MatchException>(() => Invoke(first - 1)).ErrorKind.ToString());
        Assert.Equal(expected, Invoke(first));
        Assert.Equal(expected, Encoding.UTF8.GetString(Create(pattern, 1 << 20).Replace(input, Encoding.UTF8.GetBytes(replacement), 0, options)));
    }

    [Fact]
    public void SpanReplacementDenialPreservesCallerStorageAndCombinedPolicyIsUsable()
    {
        var destination = new byte[32]; destination.AsSpan().Fill(0xA5);
        Assert.Equal("ResultLimit", Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create("A", 1).TryReplace("AzA"u8, "!"u8, destination, out _); }).ErrorKind.ToString());
        Assert.All(destination, value => Assert.Equal((byte)0xA5, value));
        var regex = new Utf8Pcre2Regex("(é)(😀)", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new()
            {
                MaxWorkSteps = 100000,
                MaxDepth = 128,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal("😀é", System.Text.Encoding.UTF8.GetString(regex.Replace("é😀"u8, "$2$1"u8)));
        ReadOnlyMemory<byte> result6Input = ("é😀"u8).ToArray();
        using var result6Cursor = regex.CreateMatchCursor(result6Input);
        result6Cursor.MoveNext(out var result6Result);
        result6Cursor.Dispose();
        Assert.Equal("é😀", System.Text.Encoding.UTF8.GetString((result6Result.Value.Success ? result6Input.Span.Slice(result6Result.Value.StartOffsetInBytes, result6Result.Value.EndOffsetInBytes - result6Result.Value.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void TemplateResultFailureIsStructuredAndFreshCallsReset()
    {
        var error = Assert.Throws<Pcre2MatchException>(() => Create(@"(?<v>\x41)😀", 1).Replace("A😀"u8, "!"u8));
        Assert.Equal("ResultLimit", error.ErrorKind.ToString());
        var regex = Create(@"(?<v>\x41)😀", 1 << 20);
        Assert.Equal("!"u8.ToArray(), regex.Replace("A😀"u8, "!"u8));
        Assert.Equal("!"u8.ToArray(), regex.Replace("A😀"u8, "!"u8));
    }
}
