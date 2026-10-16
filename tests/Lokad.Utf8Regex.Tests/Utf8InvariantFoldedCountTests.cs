using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8InvariantFoldedCountTests
{
    private const RegexOptions Invariant = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    [Fact]
    public void KelvinIsTheOnlyInvariantNonAsciiAliasOfAnAsciiNeedle()
    {
        var aliases = new HashSet<char>();
        for (var value = 0; value < 128; value++)
        {
            foreach (var equivalent in Utf8SliceCaseEquivalences.GetEquivalents(
                (char)value, Utf8ScalarCaseBehavior.Invariant))
                if (equivalent >= 128) aliases.Add(equivalent);
        }
        Assert.Equal(new[] { '\u212A' }, aliases.Order().ToArray());

        // The owned matcher compares supplementary scalars through Rune upper
        // casing. A future supplementary-to-ASCII alias must close this route.
        for (var value = 0x10000; value <= 0x10FFFF; value++)
            Assert.False(Rune.ToUpperInvariant(new Rune(value)).IsAscii);
    }

    [Theory]
    [InlineData("Sherlock Holmes|John Watson|Irene Adler|Inspector Lestrade|Professor Moriarty", "é sherlock holmes 😀 JOHN WATSON İrene Adler")]
    [InlineData("foo|bar|baz", "é FOO K BAR 😀 BAZ ſ")]
    [InlineData("k|kit|ki|s|i", "é Kit Kİ ı İ s ſ i 😀")]
    [InlineData("kit|k|ki", "é Kit KIT Kİ 🦄")]
    [InlineData("i", "é I i İ ı 😀")]
    [InlineData("k", "é K k K 😀")]
    [InlineData("s", "é S s ſ 😀")]
    [InlineData("(?<n>k|s)", "é K K s ſ 😀")]
    [InlineData(@"\b(?:k|s)\b", "éK K s ſ 😀")]
    [InlineData("(?<10>k)|(?<10>s)", "é K K s 😀")]
    public void ScalarAliasesPriorityAndBorrowedBoundsKeepTheirResults(string pattern, string text)
    {
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        {
            var options = Invariant | (compiled ? RegexOptions.Compiled : 0) | (rtl ? RegexOptions.RightToLeft : 0);
            var expected = new Regex(pattern, options).Count(text);
            var regex = new Utf8Regex(pattern, options);
            var bytes = Encoding.UTF8.GetBytes(text);
            for (var offset = 0; offset < 4; offset++)
            {
                var padded = new byte[offset + bytes.Length + 3];
                bytes.CopyTo(padded, offset);
                Assert.Equal(expected, regex.Count(padded.AsSpan(offset, bytes.Length)));
            }
            foreach (var tail in new[] { new byte[] { 255 }, new byte[] { 0xE2 }, new byte[] { 0xE2, 0x84 } })
            {
                var malformed = bytes.Concat(tail).ToArray();
                Assert.Throws<ArgumentException>(() => regex.Count(malformed));
            }
        }
    }

    [Fact]
    public void EmptyAlternativesAdvanceOverWholeScalars()
    {
        var input = Encoding.UTF8.GetBytes("é K K 😀");
        foreach (var option in new[] { RegexOptions.None, RegexOptions.Compiled,
            RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
            Assert.Equal(8, new Utf8Regex("k|", Invariant | option).Count(input));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    public void NonInvariantConstructionKeepsItsCulture(string name)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
            var oracle = new Regex("I|i|kit|s", RegexOptions.IgnoreCase);
            var regex = new Utf8Regex("I|i|kit|s", RegexOptions.IgnoreCase);
            var text = "é I i İ ı Kit ſ";
            Assert.Equal(oracle.Count(text), regex.Count(Encoding.UTF8.GetBytes(text)));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FinitePoliciesCancellationAndAbsoluteDeadlineKeepTheirAuthority(bool compiled)
    {
        var options = Invariant | (compiled ? RegexOptions.Compiled : 0);
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("é Sherlock Holmes John Watson ", 256)));
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 10000000, MaxDepth = 1024,
            MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };
        var finite = new Utf8Regex("Sherlock Holmes|John Watson", options, Regex.InfiniteMatchTimeout, policy);
        Assert.Equal(512, finite.Count(input));
        Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Regex(
            "Sherlock Holmes|John Watson", options, Regex.InfiniteMatchTimeout,
            policy with { MaxWorkSteps = 1 }).Count(input));
        var tiny = new Utf8Regex("Sherlock Holmes|John Watson", options, Regex.InfiniteMatchTimeout,
            policy with { MaxWorkingBytes = 1, MaxDepth = 1, MaxResultBytes = 1 });
        Assert.Equal(512, tiny.Count(input));
        var withKelvin = input.Concat(" K "u8.ToArray()).ToArray();
        Assert.Throws<Utf8ResourceLimitExceededException>(() => tiny.Count(withKelvin));
        using var tokens = new CancellationTokenSource();
        tokens.Cancel();
        var ordinary = new Utf8Regex("Sherlock Holmes|John Watson", options);
        Assert.Throws<OperationCanceledException>(() => ordinary.Count(input, tokens.Token));
        Assert.Throws<OperationCanceledException>(() => finite.Count(input, tokens.Token));

        var clockReads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => clockReads++ == 0 ? 0 : 25);
        var timed = new Utf8Regex("Sherlock Holmes|John Watson", options, TimeSpan.FromMilliseconds(20));
        Assert.Throws<RegexMatchTimeoutException>(() => timed.Count(input));
    }

    [Theory]
    [InlineData(2046)] [InlineData(2047)] [InlineData(2048)] [InlineData(2049)]
    public void KelvinScreeningHandlesWindowEdgesAndRetainsFallback(int offset)
    {
        var text = new string('x', offset) + "K K k";
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxDepth = 1024,
            MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 };
        var regex = new Utf8Regex("k|kit", Invariant, Regex.InfiniteMatchTimeout, policy);
        Assert.Equal(3, regex.Count(Encoding.UTF8.GetBytes(text)));
        Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Regex(
            "k|kit", Invariant, Regex.InfiniteMatchTimeout,
            policy with { MaxWorkingBytes = 1 }).Count(Encoding.UTF8.GetBytes(text)));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PostValidationCancellationKeepsTheOriginalTokenAndWork(bool compiled)
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("é Kit k ", 512)));
        using var tokens = new CancellationTokenSource();
        var validation = new Utf8SliceInvocation(new(1000000, 1, 1, 1), Regex.InfiniteMatchTimeout, tokens.Token);
        _ = Utf8Validation.Validate(input, ref validation);
        var validationSteps = validation.Budget.Steps;
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxDepth = 1,
            MaxWorkingBytes = 1, MaxResultBytes = 1 };
        var regex = new Utf8Regex("kit|k", Invariant | (compiled ? RegexOptions.Compiled : 0),
            Regex.InfiniteMatchTimeout, policy);
        ulong stoppedAt = 0;
        var session = Utf8SearchDiagnosticsSession.Start(step =>
        {
            if (step > validationSteps) { stoppedAt = step; tokens.Cancel(); }
        });
        try
        {
            var failure = Assert.Throws<OperationCanceledException>(() => regex.Count(input, tokens.Token));
            Assert.Equal(tokens.Token, failure.CancellationToken);
            Assert.True(stoppedAt > validationSteps);
        }
        finally { session.Complete(); }
    }

    [Theory]
    [InlineData("John|Jack|Irene|Inspector|Professor", 2047)]
    [InlineData("John|Jack|Irene|Inspector|Professor", 2048)]
    [InlineData(@"ab|abc|B|!|\?|@", 2047)]
    [InlineData(@"ab|abc|B|!|\?|@", 2048)]
    public void FoldedFamiliesKeepBranchPriorityAcrossCandidateWindows(string pattern, int padding)
    {
        var text = new string('j', padding) + "é JOHN ABC ! ? @ 😀 inspector ab B professor";
        var expected = new Regex(pattern, Invariant).Count(text);
        var bytes = Encoding.UTF8.GetBytes(text);
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxDepth = 1024,
            MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 };
        foreach (var compiled in new[] { false, true })
        {
            var options = Invariant | (compiled ? RegexOptions.Compiled : 0);
            var ordinary = new Utf8Regex(pattern, options);
            var finite = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout, policy);
            for (var offset = 0; offset < 3; offset++)
            {
                var padded = new byte[offset + bytes.Length + 2];
                bytes.CopyTo(padded, offset);
                Assert.Equal(expected, ordinary.Count(padded.AsSpan(offset, bytes.Length)));
                Assert.Equal(expected, finite.Count(padded.AsSpan(offset, bytes.Length)));
            }
        }
    }
}
