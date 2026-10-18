using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// Pinned CPython 3.13.2: artifacts/python-escaped-scalars-20261018/cpython.json.
public sealed class PythonReEscapedScalarTests
{
    [Theory]
    [InlineData(@"\😀", "😀", PythonReCompileOptions.None, true)]
    [InlineData(@"[\😀]", "😀", PythonReCompileOptions.None, true)]
    [InlineData(@"[\😀-\😁]+", "😀😁", PythonReCompileOptions.None, true)]
    [InlineData(@"\😀+", "😀😀", PythonReCompileOptions.None, true)]
    [InlineData(@"\é", "É", PythonReCompileOptions.IgnoreCase, true)]
    [InlineData(@"\K", "k", PythonReCompileOptions.IgnoreCase, true)]
    [InlineData(@"\𐐀", "𐐨", PythonReCompileOptions.IgnoreCase, true)]
    [InlineData(@"\𐐀", "𐐨", PythonReCompileOptions.IgnoreCase | PythonReCompileOptions.Ascii, false)]
    [InlineData(@"[\𐐀]", "𐐨", PythonReCompileOptions.IgnoreCase, true)]
    [InlineData(@"\uD83D\uDE00", "😀", PythonReCompileOptions.None, false)]
    [InlineData(@"\U0001F600", "😀", PythonReCompileOptions.None, true)]
    [InlineData(@"[^\😀]+", "😀", PythonReCompileOptions.None, false)]
    [InlineData(@"[^\😀]+", "é😁", PythonReCompileOptions.None, true)]
    [InlineData(@"\é+", "ÉÉ", PythonReCompileOptions.IgnoreCase, true)]
    [InlineData(@"\İ", "i", PythonReCompileOptions.IgnoreCase, true)]
    [InlineData(@"(?ai:\K)", "k", PythonReCompileOptions.None, false)]
    [InlineData(@"(?i:\𐐀)(?-i:\𐐀)", "𐐨𐐀", PythonReCompileOptions.None, true)]
    [InlineData(@"(?i:\𐐀)(?-i:\𐐀)", "𐐨𐐨", PythonReCompileOptions.None, false)]
    public void RawEscapesConsumeCompleteScalarsWithLexicalFolding(string pattern, string text,
        PythonReCompileOptions options, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var bytes in new[] { false, true })
        {
            var policy = new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 128,
                MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 };
            var regex = bytes
                ? new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern), options, Timeout.InfiniteTimeSpan, policy)
                : new Utf8PythonRegex(pattern, options, Timeout.InfiniteTimeSpan, policy);
            var actual = regex.FullMatchDetailedData(input);
            Assert.Equal(expected, actual.Success);
            if (expected)
            {
                Assert.Equal(text, actual.Value.ValueText);
                Assert.Equal((0, input.Length), (actual.Value.StartOffsetInBytes, actual.Value.EndOffsetInBytes));
            }
        }
    }

    [Fact]
    public void EscapedScalarRangesRetainOwnedCapturesAcrossCursorAliasesAndBulkOperations()
    {
        var input = "π😀😁é 😀"u8.ToArray();
        var regex = new Utf8PythonRegex(@"(?P<x>[\😀-\😁]+)", PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, new() { MaxWorkSteps = 100000, MaxDepth = 128,
                MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 });
        using var cancellation = new CancellationTokenSource();
        var cursor = regex.CreateMatchCursor(input, 0, cancellation.Token);
        var alias = cursor;
        Utf8PythonDetailedMatchData retained;
        try
        {
            Assert.True(cursor.MoveNext());
            retained = alias.Current;
            Assert.Equal((2, 10), (retained.Value.StartOffsetInBytes, retained.Value.EndOffsetInBytes));
            Assert.Equal((1, 5), (retained.Value.StartOffsetInUtf16, retained.Value.EndOffsetInUtf16));
            Assert.Equal(1, retained.LastIndex);
            Assert.Equal("x", retained.LastGroup);
            Assert.True(alias.MoveNext());
            Assert.Equal((13, 17), (alias.Current.Value.StartOffsetInBytes, alias.Current.Value.EndOffsetInBytes));
            Assert.False(cursor.MoveNext());
        }
        finally { alias.Dispose(); }
        Assert.Equal("😀😁", retained.Groups[1].ValueText);
        Assert.Equal(2, regex.Count(input, 0, cancellation.Token));
        var values = regex.FindAllToUtf8(input, 0, cancellation.Token);
        Assert.Equal(2, values.Count);
        Assert.True(values.ScalarValues[0].AsSpan().SequenceEqual("😀😁"u8));
        cancellation.Cancel();
        Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.Count(input, 0, cancellation.Token)).CancellationToken);
        Assert.Equal("😀😁", retained.Groups[1].ValueText);
    }

    [Fact]
    public void AValidEscapedPrefixDoesNotHideMalformedUtf8()
    {
        var regex = new Utf8PythonRegex(@"\😀");
        var input = "😀"u8.ToArray().Append((byte)0xff).ToArray();
        Assert.Throws<ArgumentException>(() => regex.Search(input));
        Assert.Throws<ArgumentException>(() => regex.Count(input));
    }

    [Fact]
    public void RepeatedEscapedScalarsKeepConstructionWorkAdmissionAndRecovery()
    {
        var pattern = string.Concat(Enumerable.Repeat(@"\😀", 1024));
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8PythonRegex.Compile(pattern,
            PythonReCompileOptions.None, Timeout.InfiniteTimeSpan, default,
            new() { MaxWorkSteps = 128 }, Timeout.InfiniteTimeSpan, CancellationToken.None));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, error.ResourceKind);
        Assert.False(Utf8ConstructionScope.IsActive);
        Assert.True(new Utf8PythonRegex(@"\😀").FullMatch("😀"u8).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EscapedScalarAcquisitionKeepsOriginalConstructionInterruptionAndUnwind(bool expire)
    {
        var pattern = string.Concat(Enumerable.Repeat(@"\😀", 1024));
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var entered = false;
        using (var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 1000000, 128))
        {
            owner.Budget.CancellationToken = cancellation.Token;
            owner.Budget.Deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(20));
            owner.Budget.CheckpointHook = steps =>
            {
                if (steps < 128) return;
                entered = true;
                if (expire) ticks = 50;
                else cancellation.Cancel();
            };
            if (expire) Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
                new PythonReParser(pattern).Parse(PythonReCompileOptions.None));
            else Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
                new PythonReParser(pattern).Parse(PythonReCompileOptions.None)).CancellationToken);
            Assert.True(entered);
            Assert.True(owner.Budget.PeakBytes > 0);
            Assert.Equal(0ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
        }
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void CorrectEscapedCapturesKeepResultAdmissionAndFreshCallRecovery()
    {
        var regex = new Utf8PythonRegex(@"(?P<x>\😀)", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 100000, MaxDepth = 128, MaxWorkingBytes = 1000000, MaxResultBytes = 1 });
        Assert.Equal(1, regex.Count("😀"u8));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.FullMatchDetailedData("😀"u8)).ResourceKind);
        Assert.Equal(1, regex.Count("😀"u8));
    }
}
