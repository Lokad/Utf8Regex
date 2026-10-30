using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SingleCategoryCountTests
{
    private const string NativeRoute = "fallback_direct_unicode_category";

    public static IEnumerable<object[]> Categories()
    {
        string[] names = ["Lu", "Ll", "Lt", "Lm", "Lo", "Mn", "Mc", "Me", "Nd", "Nl", "No", "Zs", "Zl", "Zp", "Cc", "Cf", "Cs", "Co", "Pc", "Pd", "Ps", "Pe", "Pi", "Pf", "Po", "Sm", "Sc", "Sk", "So", "Cn"];
        for (var i = 0; i < names.Length; i++) yield return [names[i], (UnicodeCategory)i];
    }

    [Theory] [MemberData(nameof(Categories))]
    public void EverySingleCategoryCountsScalarsRatherThanUtf16Units(string name, UnicodeCategory category)
    {
        var text = new StringBuilder();
        for (var i = 0; i < 128; i++) text.Append((char)i);
        text.Append("é中∑𝛁😀𐐀𐐨𝟙\u2028\u2029\u0300\u0903\u20dd\ue000");
        var random = new Random(913);
        for (var i = 0; i < 1024; i++)
        {
            var scalar = random.Next(0x110000);
            if (Rune.IsValid(scalar)) text.Append(new Rune(scalar));
        }
        var subject = text.ToString(); var input = Encoding.UTF8.GetBytes(subject);
        var expected = subject.EnumerateRunes().Count(rune => Rune.GetUnicodeCategory(rune) == category);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(@"\p{" + name + "}", options);
            var diagnostic = regex.CollectCountDiagnostics(input);
            Assert.Equal(expected, diagnostic.Result); Assert.Equal(NativeRoute, diagnostic.ExecutionRoute);
            Assert.Equal(expected, regex.Count(input)); Assert.Equal(expected, regex.Count(input, CancellationToken.None));
        }
    }

    [Theory]
    [InlineData(@"\p{Sm}")]
    [InlineData(@"(\p{Sm})")]
    [InlineData(@"(?<symbol>\p{Sm})")]
    [InlineData(@"(?:[\p{Sm}]){1}")]
    public void CompleteProofPreservesCaptureCountsAndBmpOracle(string pattern)
    {
        const string subject = "letters +<=>|~ é ∑ × £ ∞";
        var regex = new Utf8Regex(pattern);
        var diagnostic = regex.CollectCountDiagnostics(Encoding.UTF8.GetBytes(subject));
        Assert.Equal(new Regex(pattern).Count(subject), diagnostic.Result); Assert.Equal(NativeRoute, diagnostic.ExecutionRoute);
        var match = regex.Match(Encoding.UTF8.GetBytes(subject));
        Assert.True(match.Success); Assert.Equal(8, match.IndexInBytes); Assert.Equal(1, match.LengthInBytes);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(17)] [InlineData(31)]
    public void NativeBlocksKeepCompleteScalarsAndBorrowedSliceBoundaries(int offset)
    {
        var regex = new Utf8Regex(@"\p{Sm}");
        foreach (var length in new[] { 4093, 4094, 4095, 4096, 4097, 8191 })
        foreach (var scalar in new[] { "é", "∑", "𝛁", "😀" })
        {
            var text = new string('x', length) + scalar + "+" + new string('x', 4095) + "𝛁~";
            var bytes = Encoding.UTF8.GetBytes(text); var storage = Enumerable.Repeat((byte)'+', offset + bytes.Length + 11).ToArray();
            bytes.CopyTo(storage.AsSpan(offset));
            var expected = text.EnumerateRunes().Count(rune => Rune.GetUnicodeCategory(rune) == UnicodeCategory.MathSymbol);
            Assert.Equal(expected, regex.Count(storage.AsSpan(offset, bytes.Length)));
        }
    }

    [Theory]
    [InlineData(@"\p{Sm}+", RegexOptions.None)]
    [InlineData(@"\p{Sm}|a", RegexOptions.None)]
    [InlineData(@"\P{Sm}", RegexOptions.None)]
    [InlineData(@"(?i:\p{Lu})", RegexOptions.None)]
    [InlineData(@"\p{Lu}", RegexOptions.IgnoreCase)]
    [InlineData(@"\p{Sm}", RegexOptions.RightToLeft)]
    [InlineData(@"\p{Sm}", RegexOptions.NonBacktracking)]
    [InlineData(@"\p{Sm}", RegexOptions.ECMAScript)]
    public void UnprovedAndOtherExecutionContractsRetainOwnedSelection(string pattern, RegexOptions options)
    {
        const string text = "Aa +<=>~ é × ∑";
        var regex = new Utf8Regex(pattern, options); var diagnostic = regex.CollectCountDiagnostics(Encoding.UTF8.GetBytes(text));
        Assert.Equal(new Regex(pattern, options).Count(text), diagnostic.Result); Assert.NotEqual(NativeRoute, diagnostic.ExecutionRoute);
    }

    [Fact]
    public void UsefulFiniteLimitsAdmitTheConstantSpaceCounter()
    {
        var regex = new Utf8Regex(@"\p{Sm}", RegexOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        using var token = new CancellationTokenSource();
        Assert.Equal(3, regex.Count("∑𝛁+😀"u8, token.Token));
        Assert.Equal(NativeRoute, regex.CollectCountDiagnostics("∑𝛁+😀"u8).ExecutionRoute);
        var limited = new Utf8Regex(@"\p{Sm}", RegexOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.Count(new byte[8192])).ResourceKind);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void NativeWorkObservesOriginalCancellationAndDeadline(bool timeout)
    {
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var regex = new Utf8Regex(@"\p{Sm}", RegexOptions.None, timeout ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkingBytes = 1 });
        var input = new byte[4096 * 128]; var reached = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (Utf8SearchDiagnosticsSession.Current!.ExecutionRoute != Utf8ExecutionRoute.FallbackDirectUnicodeCategory) return;
            reached = true; if (timeout) ticks = 100; else token.Cancel();
        });
        try
        {
            var failure = Record.Exception(() => regex.Count(input, token.Token)); Assert.True(reached);
            if (timeout) { var error = Assert.IsType<RegexMatchTimeoutException>(failure); Assert.Empty(error.Input); Assert.Equal(regex.Pattern, error.Pattern); }
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void NativeCounterCannotOutrunFiniteWorkAfterValidation()
    {
        var input = new byte[4096 * 128]; ulong firstNative = 0;
        var reference = new Utf8Regex(@"\p{Sm}", RegexOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = 100000 });
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (firstNative == 0 && Utf8SearchDiagnosticsSession.Current!.ExecutionRoute == Utf8ExecutionRoute.FallbackDirectUnicodeCategory) firstNative = steps;
        });
        try { Assert.Equal(0, reference.Count(input)); } finally { session.Complete(); }
        Assert.True(firstNative > 1);
        var limited = new Utf8Regex(@"\p{Sm}", RegexOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = firstNative - 1 });
        session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.Count(input)).ResourceKind);
            Assert.Equal(Utf8ExecutionRoute.FallbackDirectUnicodeCategory, session.ExecutionRoute);
        }
        finally { session.Complete(); }
    }

    [Theory] [InlineData(0)] [InlineData(4095)] [InlineData(4096)]
    public void WholeSubjectValidationAndPreCancellationRemainAuthoritative(int prefix)
    {
        var regex = new Utf8Regex(@"\p{Sm}");
        var malformed = Encoding.UTF8.GetBytes(new string('+', prefix)).Concat(new byte[] { 0xf0, 0x9f }).ToArray();
        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(malformed, token.Token)).CancellationToken);
        Assert.Equal(0, regex.Count(ReadOnlySpan<byte>.Empty));
    }
}
