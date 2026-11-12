using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SingleCategoryCountTests
{
    private const string NativeRoute = "fallback_direct_unicode_category";
    private const string LetterRoute = "fallback_direct_unicode_letter";

    [Theory]
    [InlineData(@"\p{L}")] [InlineData(@"(\p{L})")] [InlineData(@"(?:[\p{L}]){1}")]
    public void LetterProofSharesBoundedAdmissionAndCountsSupplementaryScalars(string pattern)
    {
        var text = new StringBuilder();
        for (var value = 0; value < 128; value++) text.Append((char)value);
        text.Append("é中∑𝛁😀𐐀𐐨𝟙\u0345\u212a\u2028\u0300");
        foreach (var boundary in new[] { 4093, 4094, 4095, 4096, 4097 })
            text.Append(new string('x', boundary)).Append("𐐀😀é中");
        var subject = text.ToString(); var bytes = Encoding.UTF8.GetBytes(subject);
        var expected = subject.EnumerateRunes().Count(Rune.IsLetter);
        using var live = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        foreach (var finite in new[] { false, true })
        {
            var policy = finite ? new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000,
                MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 } : default;
            var regex = new Utf8Regex(pattern, options, Timeout.InfiniteTimeSpan, policy);
            Assert.Equal(expected, regex.Count(bytes));
            Assert.Equal(expected, regex.Count(bytes, live.Token));
            Assert.Equal(expected, regex.Count(bytes, live.Token, new NoStorage()));
            var diagnostic = regex.CollectCountDiagnostics(bytes);
            Assert.Equal(expected, diagnostic.Result); Assert.Equal(LetterRoute, diagnostic.ExecutionRoute);
        }
        // Count's proof does not change scalar capture materialization.
        var captured = new Utf8Regex(@"(\p{L})").MatchDetailed("😀𐐀"u8);
        Assert.Equal(4, captured.IndexInBytes); Assert.Equal(4, captured.LengthInBytes);
        Assert.Equal(4, captured.GetGroup(1).IndexInBytes); Assert.Equal(4, captured.GetGroup(1).LengthInBytes);
    }

    [Theory]
    [InlineData(@"(?i:\p{L})")] [InlineData(@"\p{L}|a")] [InlineData(@"\P{L}")]
    public void LetterRelatedGrammarPreservesScalarEnumeration(string pattern)
    {
        const string text = "Aaé中𐐀😀\u0345\u212a\u0300 1";
        foreach (var options in new[] { RegexOptions.None, RegexOptions.IgnoreCase,
            RegexOptions.NonBacktracking, RegexOptions.RightToLeft })
        {
            var regex = new Utf8Regex(pattern, options); var input = Encoding.UTF8.GetBytes(text);
            var cursor = regex.EnumerateMatches(input); var expected = 0;
            while (cursor.MoveNext()) expected++;
            Assert.Equal(expected, regex.Count(input));
        }
    }

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
        foreach (var inputValidation in Enum.GetValues<Utf8InputValidation>())
        {
            var regex = new Utf8Regex(@"\p{" + name + "}", options, inputValidation: inputValidation);
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

    [Theory]
    [InlineData(@"\p{Sm}", 0)] [InlineData(@"\p{Sm}", 1)] [InlineData(@"\p{Sm}", 17)] [InlineData(@"\p{Sm}", 31)]
    [InlineData(@"\p{L}", 0)] [InlineData(@"\p{L}", 1)] [InlineData(@"\p{L}", 17)] [InlineData(@"\p{L}", 31)]
    public void NativeBlocksKeepCompleteScalarsAndBorrowedSliceBoundaries(string pattern, int offset)
    {
        var regex = new Utf8Regex(pattern);
        foreach (var length in new[] { 4093, 4094, 4095, 4096, 4097, 8191 })
        foreach (var scalar in new[] { "é", "∑", "𝛁", "😀", "𐐀" })
        {
            var text = new string('x', length) + scalar + "+" + new string('x', 4095) + "𝛁~";
            var bytes = Encoding.UTF8.GetBytes(text); var storage = Enumerable.Repeat((byte)'+', offset + bytes.Length + 11).ToArray();
            bytes.CopyTo(storage.AsSpan(offset));
            var expected = text.EnumerateRunes().Count(rune => pattern == @"\p{L}"
                ? Rune.IsLetter(rune) : Rune.GetUnicodeCategory(rune) == UnicodeCategory.MathSymbol);
            Assert.Equal(expected, regex.Count(storage.AsSpan(offset, bytes.Length)));
        }
    }

    [Fact]
    public void FragmentedMathSymbolsRespectEveryAsciiByteAndBorrowedTails()
    {
        var text = new StringBuilder();
        for (var value = 0; value < 128; value++) text.Append((char)value).Append("é∑𝛁😀£");
        var subject = string.Concat(Enumerable.Repeat(text.ToString(), 4));
        var bytes = Encoding.UTF8.GetBytes(subject);
        var expected = subject.EnumerateRunes().Count(rune => Rune.GetUnicodeCategory(rune) == UnicodeCategory.MathSymbol);
        foreach (var validation in Enum.GetValues<Utf8InputValidation>())
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        foreach (var offset in new[] { 0, 1, 15, 31, 32, 63, 4093, 4095, 4096 })
        {
            var storage = Enumerable.Repeat((byte)'+', offset + bytes.Length + 31).ToArray();
            bytes.CopyTo(storage.AsSpan(offset));
            var regex = new Utf8Regex(@"\p{Sm}", options, inputValidation: validation);
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

    [Theory]
    [InlineData(@"\p{Sm}")] [InlineData(@"\p{L}")] [InlineData(@"\p{Lu}")]
    public void TrustedScalarCountSpendsWorkOnCountingWithoutAWidthDescription(string pattern)
    {
        var text = string.Concat(Enumerable.Repeat("∑𐐀😀A", 65536));
        var input = Encoding.UTF8.GetBytes(text);
        var expected = text.EnumerateRunes().Count(rune => pattern == @"\p{L}" ? Rune.IsLetter(rune)
            : Rune.GetUnicodeCategory(rune) == (pattern == @"\p{Sm}" ? UnicodeCategory.MathSymbol : UnicodeCategory.UppercaseLetter));
        var limits = new Utf8ResourcePolicy { MaxWorkSteps = 256, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 };
        var trusted = new Utf8Regex(pattern, RegexOptions.None, Timeout.InfiniteTimeSpan, limits,
            inputValidation: Utf8InputValidation.AssumeValid);
        using var live = new CancellationTokenSource();
        Assert.Equal(expected, trusted.Count(input, live.Token));
        Assert.Equal(expected, trusted.Count(input, live.Token, new NoStorage()));
        // The same budget still cannot cover strict whole-input validation.
        var strict = new Utf8Regex(pattern, RegexOptions.None, Timeout.InfiniteTimeSpan, limits);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => strict.Count(input, live.Token)).ResourceKind);
    }

    [Theory]
    [InlineData(@"\p{Sm}")] [InlineData(@"\p{L}")] [InlineData(@"\p{Lu}")]
    public void NativeCountUsesTheSelectedValidationAuthorityOnce(string pattern)
    {
        var input = Encoding.UTF8.GetBytes(new string('x', 4095) + "∑𐐀😀A");
        foreach (var policy in Enum.GetValues<Utf8InputValidation>())
        {
            var regex = new Utf8Regex(pattern, inputValidation: policy);
            var session = Utf8SearchDiagnosticsSession.Start();
            try
            {
                regex.Count(input);
                Assert.Equal(policy == Utf8InputValidation.Validate ? 1 : 0, session.SubjectValidations);
                Assert.Equal(pattern == @"\p{L}" ? Utf8ExecutionRoute.FallbackDirectUnicodeLetter
                    : Utf8ExecutionRoute.FallbackDirectUnicodeCategory, session.ExecutionRoute);
            }
            finally { session.Complete(); }
        }
    }

    [Theory]
    [InlineData(@"\p{Sm}", false)] [InlineData(@"\p{Sm}", true)]
    [InlineData(@"\p{L}", false)] [InlineData(@"\p{L}", true)]
    public void NativeWorkObservesOriginalCancellationAndDeadline(string pattern, bool timeout)
    {
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var regex = new Utf8Regex(pattern, RegexOptions.None, timeout ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkingBytes = 1 });
        var input = new byte[4096 * 128]; var reached = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (Utf8SearchDiagnosticsSession.Current!.ExecutionRoute != (pattern == @"\p{L}"
                ? Utf8ExecutionRoute.FallbackDirectUnicodeLetter : Utf8ExecutionRoute.FallbackDirectUnicodeCategory)) return;
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

    [Theory] [InlineData(@"\p{Sm}")] [InlineData(@"\p{L}")]
    public void NativeCounterCannotOutrunFiniteWorkAfterValidation(string pattern)
    {
        var input = new byte[4096 * 128]; ulong firstNative = 0;
        var route = pattern == @"\p{L}" ? Utf8ExecutionRoute.FallbackDirectUnicodeLetter : Utf8ExecutionRoute.FallbackDirectUnicodeCategory;
        var reference = new Utf8Regex(pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = 100000 });
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (firstNative == 0 && Utf8SearchDiagnosticsSession.Current!.ExecutionRoute == route) firstNative = steps;
        });
        try { Assert.Equal(0, reference.Count(input)); } finally { session.Complete(); }
        Assert.True(firstNative > 1);
        var limited = new Utf8Regex(pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = firstNative - 1 });
        session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.Count(input)).ResourceKind);
            Assert.Equal(route, session.ExecutionRoute);
        }
        finally { session.Complete(); }
    }

    [Theory]
    [InlineData(@"\p{Sm}", 0)] [InlineData(@"\p{Sm}", 4095)] [InlineData(@"\p{Sm}", 4096)]
    [InlineData(@"\p{L}", 0)] [InlineData(@"\p{L}", 4095)] [InlineData(@"\p{L}", 4096)]
    public void WholeSubjectValidationAndPreCancellationRemainAuthoritative(string pattern, int prefix)
    {
        var regex = new Utf8Regex(pattern);
        var malformed = Encoding.UTF8.GetBytes(new string('+', prefix)).Concat(new byte[] { 0xf0, 0x9f }).ToArray();
        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(malformed, token.Token)).CancellationToken);
        Assert.Equal(0, regex.Count(ReadOnlySpan<byte>.Empty));
    }

    private sealed class NoStorage : IUtf8MemoryAllowance
    {
        public bool TryReserve(ulong bytes) => bytes == 0;
        public void Release(ulong bytes) => Assert.Equal(0UL, bytes);
    }
}
