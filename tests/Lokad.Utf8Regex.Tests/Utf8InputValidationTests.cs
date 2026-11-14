using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8InputValidationTests
{
    [Theory]
    [InlineData(@"\b\w{10,}\b", "é😀 abcdefghijkl éééééééééé", true, 2)]
    [InlineData(@"\b\w{10,}\b", "é😀 short", true, 0)]
    [InlineData(@"(\d{1,2})X", "é😀 𝟙𝟚X", false, 1)]
    [InlineData(@"(\d{1,2})X", "é😀 𝟙𝟚Y", false, 0)]
    [InlineData(@"^[-+]?\d*\.?\d*$", "𝟙.٢", false, 1)]
    [InlineData(@"^[-+]?\d*\.?\d*$", "x𝟙.٢", false, 0)]
    public void EligibilityGatesPreserveMixedScalarResultsAndOneValidation(
        string pattern, string text, bool count, int expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        byte[] storage = [0xff, 0xff, 0xff, .. input, 0xff];
        using var source = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        foreach (var validation in new[] { Utf8InputValidation.Validate, Utf8InputValidation.AssumeValid })
        {
            var regex = new Utf8Regex(pattern, options, resourcePolicy: new()
                { MaxWorkSteps = 100000, MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 },
                inputValidation: validation);
            var session = Utf8SearchDiagnosticsSession.Start();
            try
            {
                var result = count ? regex.Count(storage.AsSpan(3, input.Length), source.Token)
                    : regex.IsMatch(storage.AsSpan(3, input.Length), source.Token) ? 1 : 0;
                Assert.Equal(expected, result);
                Assert.Equal(validation == Utf8InputValidation.Validate ? 1 : 0, session.SubjectValidations);
            }
            finally { session.Complete(); }
            if (validation == Utf8InputValidation.AssumeValid) continue;
            var error = Assert.Throws<ArgumentException>(() =>
            {
                if (count) regex.Count(storage.AsSpan(3), source.Token);
                else regex.IsMatch(storage.AsSpan(3), source.Token);
            });
            Assert.Contains($"byte offset {input.Length}.", error.Message);
        }
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void TrustedEarlyBooleanNeedsNoUnrelatedSuffixDescription(RegexOptions options)
    {
        var input = Encoding.UTF8.GetBytes("12X é" + new string('!', 1 << 20));
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 128, MaxDepth = 4096,
            MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };
        var trusted = new Utf8Regex(@"(\d{1,2})X", options, resourcePolicy: policy,
            inputValidation: Utf8InputValidation.AssumeValid);
        Assert.True(trusted.IsMatch(input));
        var strict = new Utf8Regex(@"(\d{1,2})X", options, resourcePolicy: policy);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => strict.IsMatch(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
    }

    [Theory]
    [InlineData("needle", "needle é😀 needle")]
    [InlineData("café|😀", "z😀 café!")]
    [InlineData("(a+)(b*)", "é😀 aabb aa")]
    [InlineData("", "é😀x")]
    [InlineData(@"\p{L}+", "é😀abc 中文")]
    [InlineData("k", "K k K")]
    [InlineData("Tom.{1,3}river", "é Tom😀river! Tomabriver")]
    [InlineData("(?<word>needle)", "needle é😀 needle")]
    public void PoliciesPreservePublicResultsAndValidateAtMostOnce(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.NonBacktracking,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant })
        foreach (var finite in new[] { false, true })
        {
            var strict = new Utf8Regex(pattern, options, resourcePolicy: finite ? new() { MaxWorkSteps = 1000000 } : default);
            var assumed = new Utf8Regex(pattern, options, resourcePolicy: strict.ResourcePolicy, inputValidation: Utf8InputValidation.AssumeValid);
            Assert.Equal(Utf8InputValidation.Validate, strict.InputValidation);
            Assert.Equal(Utf8InputValidation.AssumeValid, assumed.InputValidation);
            Assert.Equal(Observe(strict, () => strict.IsMatch(input)), Observe(assumed, () => assumed.IsMatch(input)));
            Assert.Equal(Observe(strict, () => strict.Count(input)), Observe(assumed, () => assumed.Count(input)));
            Assert.Equal(Observe(strict, () => strict.Match(input)), Observe(assumed, () => assumed.Match(input)));
            Assert.Equal(Observe(strict, () => strict.MatchDetailed(input).Success), Observe(assumed, () => assumed.MatchDetailed(input).Success));
            Assert.Equal(Observe(strict, () => strict.Replace(input, "X")), Observe(assumed, () => assumed.Replace(input, "X")));
            Assert.Equal(Observe(strict, () => strict.Replace(input, "X"u8)), Observe(assumed, () => assumed.Replace(input, "X"u8)));
            Assert.Equal(Observe(strict, () => strict.ReplaceToString(input, "X")), Observe(assumed, () => assumed.ReplaceToString(input, "X")));
            if (pattern.Contains("word", StringComparison.Ordinal))
                Assert.Equal(Observe(strict, () => strict.Replace(input, "${word}")), Observe(assumed, () => assumed.Replace(input, "${word}")));
            Assert.Equal(Observe(strict, () => Splits(strict, input)), Observe(assumed, () => Splits(assumed, input)));
            Assert.Equal(Observe(strict, () => Matches(strict, input)), Observe(assumed, () => Matches(assumed, input)));
            Assert.Equal(strict.Count(input), Observe(assumed, () => CursorCount(assumed, input)));
        }
    }

    [Theory]
    [InlineData("needle")]
    [InlineData("(n.*)")]
    [InlineData("")]
    public void DefaultRejectsMalformedSuffixForEveryOperation(string pattern)
    {
        byte[] input = [.. "needle"u8, 0xff];
        var regex = new Utf8Regex(pattern);
        Assert.Throws<ArgumentException>(() => regex.IsMatch(input));
        Assert.Throws<ArgumentException>(() => regex.Count(input));
        Assert.Throws<ArgumentException>(() => regex.Match(input));
        Assert.Throws<ArgumentException>(() => regex.MatchDetailed(input));
        Assert.Throws<ArgumentException>(() => regex.Replace(input, "x"));
        Assert.Throws<ArgumentException>(() => Splits(regex, input));
        Assert.Throws<ArgumentException>(() => Matches(regex, input));
        Assert.Throws<ArgumentException>(() => regex.CreateMatchCursor(input));
    }

    [Fact]
    public void FactoriesKeepPatternTemplateAndCancellationContracts()
    {
        var regex = Utf8Regex.Compile("needle"u8, inputValidation: Utf8InputValidation.AssumeValid);
        Assert.Equal(Utf8InputValidation.AssumeValid, regex.InputValidation);
        Assert.Throws<ArgumentException>(() => Utf8Regex.Compile(new byte[] { 0xff }, inputValidation: Utf8InputValidation.AssumeValid));
        Assert.Throws<ArgumentException>(() => regex.Replace("needle"u8, new byte[] { 0xff }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Regex("x", inputValidation: (Utf8InputValidation)10));
        using var source = new CancellationTokenSource(); source.Cancel();
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => regex.Count("needle"u8, source.Token)).CancellationToken);
        // Existing short overloads, including bare defaults, retain their binding.
        Assert.Equal(Utf8InputValidation.Validate, new Utf8Regex("x", default).InputValidation);
    }

    [Fact]
    public void FundedFactoryPropagatesPolicyThroughCountCursorAndReplacement()
    {
        var account = new Account();
        using (var owner = Utf8Regex.Compile("needle", RegexOptions.None, Timeout.InfiniteTimeSpan,
            default, default, Timeout.InfiniteTimeSpan, default, account, inputValidation: Utf8InputValidation.AssumeValid))
        {
            Assert.Equal(Utf8InputValidation.AssumeValid, owner.InputValidation);
            Assert.Equal(2, owner.Count("needle é😀 needle"u8));
            using (var cursor = owner.CreateMatchCursor("needle é😀 needle"u8.ToArray()))
            {
                Assert.True(cursor.MoveNext(out var retained));
                Assert.True(cursor.MoveNext(out _));
                Assert.False(cursor.MoveNext(out _));
                Assert.True(retained.Success);
            }
            Span<byte> destination = stackalloc byte[64];
            Assert.Equal(System.Buffers.OperationStatus.Done,
                owner.TryReplace("needle é😀 needle"u8, "X"u8, destination, out var written));
            Assert.Equal("X é😀 X", Encoding.UTF8.GetString(destination[..written]));
        }
        Assert.Equal(0UL, account.Used);
    }

    private sealed class Account : IUtf8MemoryAllowance
    {
        internal ulong Used;
        public bool TryReserve(ulong bytes) { Used = checked(Used + bytes); return true; }
        public void Release(ulong bytes) { Assert.True(bytes <= Used); Used -= bytes; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AssumeValidRetainsInterruptionDuringMatching(bool deadline)
    {
        long now = 0;
        using var clock = Internal.Execution.Utf8ExecutionDeadline.UseClockForDiagnostics(() => now);
        using var source = new CancellationTokenSource();
        var regex = new Utf8Regex("needle", matchTimeout: deadline ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan,
            inputValidation: Utf8InputValidation.AssumeValid);
        var bytes = Encoding.UTF8.GetBytes(new string('x', 256 * 1024) + "needle");
        var triggered = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ => { triggered = true; if (deadline) now = 101; else source.Cancel(); });
        try
        {
            if (deadline) Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(bytes, source.Token));
            else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(bytes, source.Token)).CancellationToken);
            Assert.True(triggered);
            Assert.Equal(0, session.SubjectValidations);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void AssumeValidRetainsFiniteWorkAndWorkingAdmission()
    {
        var work = new Utf8Regex(@"(a+)\1", resourcePolicy: new() { MaxWorkSteps = 1 }, inputValidation: Utf8InputValidation.AssumeValid);
        Assert.Throws<Utf8ResourceLimitExceededException>(() => work.Count("aaaa"u8));
        var memory = new Utf8Regex(@"(a+)\1", resourcePolicy: new() { MaxWorkingBytes = 1 }, inputValidation: Utf8InputValidation.AssumeValid);
        Assert.Throws<Utf8ResourceLimitExceededException>(() => memory.MatchDetailed("aaaa"u8));
    }

    [Theory]
    [InlineData("needle")]
    [InlineData("(a+)(b*)")]
    public void OffsetOperationsShareTheSelectedSubjectAuthority(string pattern)
    {
        var bytes = Encoding.UTF8.GetBytes("é😀 needle aabb needle");
        var strict = new Utf8Regex(pattern);
        var assumed = new Utf8Regex(pattern, inputValidation: Utf8InputValidation.AssumeValid);
        Assert.Equal(Observe(strict, () => strict.IsMatchFromUtf16Offset(bytes, 3)), Observe(assumed, () => assumed.IsMatchFromUtf16Offset(bytes, 3)));
        Assert.Equal(Observe(strict, () => strict.CountFromUtf16Offset(bytes, 3)), Observe(assumed, () => assumed.CountFromUtf16Offset(bytes, 3)));
        Assert.Equal(Observe(strict, () => strict.MatchFromUtf16Offset(bytes, 3)), Observe(assumed, () => assumed.MatchFromUtf16Offset(bytes, 3)));
        Assert.Equal(Observe(strict, () => strict.MatchDetailedFromUtf16Offset(bytes, 3).Success), Observe(assumed, () => assumed.MatchDetailedFromUtf16Offset(bytes, 3).Success));
    }

    private static T Observe<T>(Utf8Regex regex, Func<T> operation)
    {
        var session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            var result = operation();
            Assert.InRange(session.SubjectValidations, 0, regex.InputValidation == Utf8InputValidation.Validate ? 1 : 0);
            return result;
        }
        finally { session.Complete(); }
    }

    private static string[] Splits(Utf8Regex regex, byte[] input)
    {
        var result = new List<string>();
        foreach (var split in regex.EnumerateSplits(input)) result.Add(split.GetValueString());
        return result.ToArray();
    }

    private static Utf8ValueMatch[] Matches(Utf8Regex regex, byte[] input)
    {
        var result = new List<Utf8ValueMatch>();
        foreach (var match in regex.EnumerateMatches(input)) result.Add(match);
        return result.ToArray();
    }

    private static int CursorCount(Utf8Regex regex, byte[] input)
    {
        using var cursor = regex.CreateMatchCursor(input);
        var count = 0;
        while (cursor.MoveNext(out _)) count++;
        return count;
    }
}
