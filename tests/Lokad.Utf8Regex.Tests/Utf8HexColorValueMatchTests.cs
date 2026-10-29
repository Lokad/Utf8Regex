using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8HexColorValueMatchTests
{
    private const string Color = @"^#?([a-f0-9]{6}|[a-f0-9]{3})$";
    private const RegexOptions Fold = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly Utf8ResourcePolicy Funded = new()
    {
        MaxWorkSteps = 100000, MaxDepth = 256, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20
    };

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompleteGrammarSelectsTheScalarRangeOnOffsetSpans(bool compiled)
    {
        foreach (var pattern in new[] { Color, Color.Replace("{6}|[a-f0-9]{3}", "{3}|[a-f0-9]{6}"),
            @"\A#?(?<color>[A-Fa-f0-9]{3}|[A-Fa-f0-9]{6})\Z" })
        {
            var options = Fold | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var regex = new Utf8Regex(pattern, options, Timeout.InfiniteTimeSpan, Funded);
            var oracle = new Regex(pattern, options);
            Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredAsciiHexColorWhole,
                regex.Inspection.PreparedRegex.FallbackDirectFamily.Kind);
            foreach (var text in new[] { "", "#", "ab", "abc", "ABC", "#aBc", "abcd", "abcdef", "#ABC123",
                "#abcd", "aBc123\n", "#abc\n", "abc\r\n", "abc\n\n", "abcdefg", "abc def", "#12g", "abc\0" })
            {
                var input = Encoding.ASCII.GetBytes(text); byte[] storage = [0xff, .. input, 0xff];
                var expected = regex.MatchDetailed(input); var bcl = oracle.Match(text);
                Assert.Equal(bcl.Success, expected.Success);
                if (bcl.Success) Assert.Equal(bcl.Length, expected.LengthInUtf16);
                using var token = new CancellationTokenSource();
                var session = Utf8SearchDiagnosticsSession.Start();
                try
                {
                    var actual = regex.Match(storage.AsSpan(1, input.Length), token.Token);
                    Assert.Equal(expected.Success, actual.Success);
                    if (actual.Success) Assert.Equal((expected.IndexInBytes, expected.LengthInBytes),
                        (actual.IndexInBytes, actual.LengthInBytes));
                    Assert.Equal(Utf8ExecutionRoute.NativeAsciiAnchoredValidator, session.ExecutionRoute);
                }
                finally { session.Complete(); }
            }
        }
    }

    [Fact]
    public void ProofNearMissesUnicodeOffsetsAndExcludedModesKeepScalarResults()
    {
        foreach (var pattern in new[] { Color, @"^#?([a-f0-9]{6}|[a-f0-9]{3})\z", @"^#([a-f0-9]{6}|[a-f0-9]{3})$",
            @"^#?([a-g0-9]{6}|[a-g0-9]{3})$", @"^#?((?-i:[a-f0-9]){6}|(?-i:[a-f0-9]){3})$" })
        foreach (var options in new[] { Fold, Fold | RegexOptions.NonBacktracking, Fold | RegexOptions.RightToLeft })
        {
            var regex = new Utf8Regex(pattern, options);
            foreach (var text in new[] { "abc", "#ABC123", "ggg", "ABC\n", "abc\nrest", "😀abc", "abc😀", "ébc", "１２３" })
            {
                var input = Encoding.UTF8.GetBytes(text); var expected = regex.MatchDetailed(input); var actual = regex.Match(input);
                Assert.Equal(expected.Success, actual.Success);
                if (actual.Success) Assert.Equal((expected.IndexInBytes, expected.LengthInBytes),
                    (actual.IndexInBytes, actual.LengthInBytes));
            }
        }
        var offset = new Utf8Regex(Color, Fold); var bytes = "#abc"u8.ToArray();
        Assert.True(offset.MatchFromUtf16Offset(bytes, 0).Success);
        Assert.False(offset.MatchFromUtf16Offset(bytes, 1).Success);
        Assert.False(offset.Match(new byte[2049]).Success);
        var original = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "en-US", "tr-TR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var regex = new Utf8Regex(Color, RegexOptions.IgnoreCase);
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                Assert.Equal(7, regex.Match("#AbC123\n"u8).LengthInBytes);
            }
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void ValueRangeNeedsNoCaptureWorkspaceButOriginalWorkAdmissionRemains()
    {
        var tight = new Utf8Regex(Color, Fold, Timeout.InfiniteTimeSpan,
            Funded with { MaxWorkingBytes = 1, MaxResultBytes = 1 });
        Assert.Equal(7, tight.Match("#ABC123"u8).LengthInBytes);
        var denied = new Utf8Regex(Color, Fold, Timeout.InfiniteTimeSpan, Funded with { MaxWorkSteps = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Match("#ABC123"u8)).ResourceKind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalInterruptionIsObservedAfterValidation(bool expire)
    {
        var input = "#ABC123"u8.ToArray();
        var timeout = TimeSpan.FromMilliseconds(20); var validationReads = 0;
        using (Utf8ExecutionDeadline.UseClockForDiagnostics(() => { validationReads++; return 0; }))
        {
            var validation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, timeout, default);
            Utf8Validation.Validate(input, ref validation);
        }
        using var token = new CancellationTokenSource(); var reads = 0; var entered = false;
        // This constant-width operation never reaches the 64-step diagnostic
        // hook. Inject through the captured clock after validation's checks.
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads <= validationReads) return 0;
            entered = true;
            if (expire) return 50;
            token.Cancel(); return 0;
        });
        var regex = new Utf8Regex(Color, Fold, timeout, Funded);
        if (expire) Assert.Equal(regex.MatchTimeout,
            Assert.Throws<RegexMatchTimeoutException>(() => regex.Match(input, token.Token)).MatchTimeout);
        else Assert.Equal(token.Token,
            Assert.Throws<OperationCanceledException>(() => regex.Match(input, token.Token)).CancellationToken);
        Assert.True(entered);
    }

    [Fact]
    public void DetailedCapturesRetainedAliasesAndStrictInputStayOwned()
    {
        var regex = new Utf8Regex(Color, Fold); var input = "#AbC123\n"u8.ToArray();
        var detailed = regex.MatchDetailed(input); var group = detailed.GetGroup(1);
        var value = regex.Match(input); var cursor = regex.EnumerateMatches(input); var alias = cursor;
        Assert.True(cursor.MoveNext()); Assert.True(alias.MoveNext());
        Assert.Equal(value, cursor.Current); Assert.Equal(value, alias.Current);
        Assert.False(cursor.MoveNext()); Assert.False(alias.MoveNext());
        Assert.Equal("AbC123", group.GetValueString()); Assert.Equal("#AbC123", detailed.GetValueString());
        byte[] malformed = [.. input, 0xff];
        Assert.Throws<ArgumentException>(() => regex.Match(malformed));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token,
            Assert.Throws<OperationCanceledException>(() => regex.Match(malformed, token.Token)).CancellationToken);
    }
}
