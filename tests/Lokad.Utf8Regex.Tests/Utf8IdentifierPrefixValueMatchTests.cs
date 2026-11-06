using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8IdentifierPrefixValueMatchTests
{
    private const string Pattern = @"\G[a-z][a-z0-9_]*";
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant;
    private static readonly Utf8ResourcePolicy Policy = new()
    { MaxWorkSteps = 1_000_000, MaxDepth = 256, MaxWorkingBytes = 1_000_000, MaxResultBytes = 1_000_000 };

    [Fact]
    public void BoundedFoldedAsciiValuesUseTheExistingCompletePrefixProof()
    {
        using var live = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(Pattern, Options | mode, Timeout.InfiniteTimeSpan,
                Policy with { MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
            Assert.Equal("AnchoredIdentifierPrefix", regex.Inspection.DebugFallbackDirectFamilyKind);
            var oracle = new Regex(Pattern, Options | mode);
            foreach (var text in new[] { "", "_abc", "9abc", "a", "I_i0", "AbC123_DEF-tail", "a\0tail", "a\nZ", "\nabc" })
            {
                var input = Encoding.ASCII.GetBytes(text); byte[] backing = [0xff, .. input, 0xff];
                var session = Utf8SearchDiagnosticsSession.Start();
                try
                {
                    var value = regex.Match(backing.AsSpan(1, input.Length), live.Token);
                    var expected = oracle.Match(text); Assert.Equal(expected.Success, value.Success);
                    if (value.Success) Assert.Equal((0, expected.Length, 0, expected.Length),
                        (value.IndexInBytes, value.LengthInBytes, value.IndexInUtf16, value.LengthInUtf16));
                    Assert.Equal(Utf8ExecutionRoute.NativeAsciiAnchoredValidator, session.ExecutionRoute);
                    Assert.Equal(value, regex.Match(input));
                }
                finally { session.Complete(); }
            }
        }
    }

    [Fact]
    public void CultureCapturedDuringConstructionKeepsTurkicAndUnicodeSemantics()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "en-US", "tr-TR" })
            foreach (var invariant in new[] { false, true })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var options = RegexOptions.IgnoreCase | (invariant ? RegexOptions.CultureInvariant : RegexOptions.None);
                var regex = new Utf8Regex(Pattern, options); var oracle = new Regex(Pattern, options);
                Assert.Equal(culture == "tr-TR" && !invariant ? "None" : "AnchoredIdentifierPrefix",
                    regex.Inspection.DebugFallbackDirectFamilyKind);
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture == "tr-TR" ? "en-US" : "tr-TR");
                // The shared scalar fixture recompiles its oracle under the
                // current culture. These classes consume only BMP scalars, so
                // compare the original captured-culture Regex directly.
                foreach (var text in new[] { "I", "i", "İ", "ı", "K", "K", "I_i0", "İx", "aK1", "a😀tail", "😀I" })
                {
                    var input = Encoding.UTF8.GetBytes(text); var expected = oracle.Match(text);
                    Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
                    Assert.Equal(oracle.Count(text), regex.Count(input));
                    var actual = regex.Match(input);
                    Assert.True(expected.Success == actual.Success, $"{culture}/{invariant}: {text}");
                    if (actual.Success) Assert.Equal((expected.Index, expected.Length,
                        Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), Encoding.UTF8.GetByteCount(expected.Value)),
                        (actual.IndexInUtf16, actual.LengthInUtf16, actual.IndexInBytes, actual.LengthInBytes));
                    Assert.Equal(expected.Value, regex.MatchDetailed(input).GetGroup(0).GetValueString());
                }
            }
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    [Theory]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)]
    public void LongSubjectsRetainTightWorkingAdmission(int length)
    {
        var input = Encoding.ASCII.GetBytes(new string('A', length));
        Assert.Equal(length, new Utf8Regex(Pattern, Options).Match(input).LengthInBytes);
        var tight = new Utf8Regex(Pattern, Options, Timeout.InfiniteTimeSpan,
            Policy with { MaxWorkingBytes = 1, MaxResultBytes = 1 });
        // The scalar continuation also needs no mutable repeat buffers.
        Assert.Equal(length, tight.Match(input).LengthInBytes);
    }

    [Fact]
    public void CapturesAliasesAndDeclinedShapesRemainValid()
    {
        foreach (var pattern in new[] { Pattern, Pattern + "x", @"\G[a-z][a-z0-9_]*?" })
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft })
            Utf8SliceSparseGroupTests.AssertOwnedOperations(new Utf8Regex(pattern, Options | mode), new Regex(pattern, Options | mode),
                ["AbC12x_tail", "Kabc_9", "a😀tail", "no match", "0bad"]);
        var captured = new Utf8Regex(@"\G(?<id>[a-z][a-z0-9_]*)", Options);
        var bytes = "AbC_9!tail"u8.ToArray(); var retained = captured.MatchDetailed(bytes);
        var cursor = captured.EnumerateMatches(bytes); var alias = cursor;
        Assert.True(cursor.MoveNext()); Assert.True(alias.MoveNext()); var first = alias.Current;
        Assert.Equal(first, cursor.Current); Assert.False(cursor.MoveNext()); Assert.Equal(first, alias.Current);
        _ = captured.Match("Other"u8); Assert.Equal("AbC_9", retained.GetGroup("id").GetValueString());
        var offset = new Utf8Regex(Pattern, Options).MatchFromUtf16Offset("😀!AbC9!tail"u8, 3);
        Assert.Equal((5, 4, 3, 4), (offset.IndexInBytes, offset.LengthInBytes, offset.IndexInUtf16, offset.LengthInUtf16));
        var nbPattern = Pattern.Replace(@"\G", @"\A");
        Utf8SliceSparseGroupTests.AssertOwnedOperations(new Utf8Regex(nbPattern, Options | RegexOptions.NonBacktracking),
            new Regex(nbPattern, Options | RegexOptions.NonBacktracking), ["AbC_9!", "Kabc", "0bad"]);
    }

    [Fact]
    public void FullValidationAndOriginalEntryCancellationRemainStrict()
    {
        var regex = new Utf8Regex(Pattern, Options);
        foreach (var text in new[] { "AbC9", "0bad" })
        {
            byte[] invalid = [.. Encoding.ASCII.GetBytes(text), 0xff];
            Assert.Throws<ArgumentException>(() => regex.Match(invalid));
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => regex.Match(invalid, canceled.Token)).CancellationToken);
        }
    }

    [Theory]
    [InlineData("AbC_9-tail", true)] [InlineData("0bad", false)]
    public void EveryWorkCutoffRefusesPartialPublication(string text, bool success)
    {
        var input = Encoding.ASCII.GetBytes(text); var denied = false; var admitted = false;
        for (ulong work = 1; work < 100; work++)
        {
            var regex = new Utf8Regex(Pattern, Options, Timeout.InfiniteTimeSpan,
                Policy with { MaxWorkSteps = work, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
            try { Assert.Equal(success, regex.Match(input).Success); admitted = true; break; }
            catch (Utf8ResourceLimitExceededException failure)
            { Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind); denied = true; }
        }
        Assert.True(denied); Assert.True(admitted);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PublicationObservesTheOriginalTokenAndDeadline(bool expire)
    {
        var reads = 0; var trigger = int.MaxValue; long ticks = 0;
        using var live = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        { if (++reads == trigger) { if (expire) ticks = 100; else live.Cancel(); } return ticks; });
        var regex = new Utf8Regex(Pattern, Options, TimeSpan.FromMilliseconds(50), Policy);
        Assert.True(regex.Match("AbC_9-tail"u8, live.Token).Success);
        trigger = reads; reads = 0;
        if (expire) Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.Match("AbC_9-tail"u8, live.Token)).MatchTimeout);
        else Assert.Equal(live.Token, Assert.Throws<OperationCanceledException>(() => regex.Match("AbC_9-tail"u8, live.Token)).CancellationToken);
    }
}
