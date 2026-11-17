using System.Runtime.Intrinsics;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class FusedAsciiBoundaryLiteralFamilyCountTests
{
    private const string Pattern = @"\b(?:LogTrace|LogDebug|LogInformation|LogWarning|LogError)\b";

    [Theory]
    [InlineData(Pattern)]
    [InlineData(@"\b(?:Task|TaskFactory|TaskScheduler)\b")]
    [InlineData(@"\B(?:foo|foobar)\B")]
    [InlineData(@"(?<word>\b(?:LogTrace|LogDebug|LogInformation|LogWarning|LogError)\b)")]
    public void SharedPrefixLiteralsPreserveBoundariesPriorityAndCaptures(string pattern)
    {
        using var source = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft,
            RegexOptions.NonBacktracking, RegexOptions.IgnoreCase | RegexOptions.Compiled })
        foreach (var controlled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | mode;
            var regex = new Utf8Regex(pattern, options, resourcePolicy: controlled
                ? new() { MaxWorkSteps = 1_000_000, MaxDepth = 1024, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 }
                : default);
            var oracle = new Regex(pattern, options);
            var token = controlled ? source.Token : default;
            // BMP word neighbors and nonword emoji share the scalar boundary
            // semantics of this literal-only grammar with the BCL comparator.
            foreach (var text in new[] { "", "LogTrace xLogDebug LogInformation_ LogWarning LogError",
                "TaskFactory TaskScheduler Task _foo_ _foobar_ xfoox", "éLogTrace LogDebugé α LogError 😀 LogWarning.\u0301",
                "logtrace LOGERROR TasK Task LogWarning", "😀 Task LogError xfoo_" })
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                var expected = oracle.Matches(text);
                Assert.Equal(expected.Count, regex.Count(bytes, token));
                Assert.Equal(expected.Count != 0, regex.IsMatch(bytes, token));
                using var cursor = regex.CreateMatchCursor(bytes, cancellationToken: token);
                foreach (Match match in expected)
                {
                    Assert.True(cursor.MoveNext(out var actual));
                    Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, match.Index)), actual.Value.StartOffsetInBytes);
                    Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, match.Index + match.Length)), actual.Value.EndOffsetInBytes);
                    for (var group = 0; group < match.Groups.Count; group++)
                    {
                        Assert.Equal(match.Groups[group].Value, Encoding.UTF8.GetString(actual.GetGroupMemory(group).Span));
                        Assert.Equal(match.Groups[group].Captures.Count, actual.GetCaptures(group).Length);
                    }
                }
                Assert.False(cursor.MoveNext(out _));
            }
        }
    }

    [Fact]
    public void CompiledPascalCaseFamilyFusesAsciiValidationWithCount()
    {
        var regex = new Utf8Regex(Pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var input = Encoding.ASCII.GetBytes("LogTrace λ LogDebug_ LogInformation. LogWarning LogError");

        if (Vector256.IsHardwareAccelerated)
        {
            Assert.True(regex.Inspection.DebugHasFusedAsciiBoundaryLiteralFamilyCount);
        }

        Assert.Equal(4, regex.Count(input));

        if (Vector256.IsHardwareAccelerated)
        {
            Assert.Equal(
                "compiled_fused_ascii_literal_family_count",
                regex.CollectCountDiagnostics(input).ExecutionRoute);
        }
    }

    [Fact]
    public void FusedCountCoversEveryVectorLaneAndTheScalarTail()
    {
        var regex = new Utf8Regex(Pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var expectedRegex = new Regex(
            Pattern,
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            Regex.InfiniteMatchTimeout);

        foreach (var borrowedOffset in new[] { 0, 1, 17, 31 })
        for (var offset = 0; offset < 96; offset++)
        {
            var storage = Enumerable.Repeat((byte)0xFF, borrowedOffset + 128 + 11).ToArray();
            var input = storage.AsSpan(borrowedOffset, 128);
            input.Fill((byte)'.');
            "LogInformation"u8.CopyTo(input[offset..]);

            Assert.Equal(expectedRegex.Count(Encoding.ASCII.GetString(input)), regex.Count(input));
        }
    }

    [Fact]
    public void FusedCountMatchesDotNetOnDeterministicAsciiSubjects()
    {
        var regex = new Utf8Regex(Pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var expectedRegex = new Regex(
            Pattern,
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            Regex.InfiniteMatchTimeout);
        var random = new Random(0x51A7C11);
        string[] inserts =
        [
            "LogTrace",
            "LogDebug",
            "LogInformation",
            "LogWarning",
            "LogError",
            "xLogTrace",
            "LogError_",
            "LogUnknown",
        ];

        for (var subjectIndex = 0; subjectIndex < 256; subjectIndex++)
        {
            var input = new byte[random.Next(1, 513)];
            random.NextBytes(input);
            for (var i = 0; i < input.Length; i++)
            {
                input[i] = input[i] switch
                {
                    < 26 => (byte)('A' + input[i]),
                    < 52 => (byte)('a' + input[i] - 26),
                    < 62 => (byte)('0' + input[i] - 52),
                    < 70 => (byte)'_',
                    _ => (byte)'.',
                };
            }

            var insert = Encoding.ASCII.GetBytes(inserts[random.Next(inserts.Length)]);
            if (insert.Length <= input.Length)
            {
                insert.CopyTo(input, random.Next(input.Length - insert.Length + 1));
            }

            Assert.Equal(expectedRegex.Count(Encoding.ASCII.GetString(input)), regex.Count(input));
        }
    }

    [Fact]
    public void ValidNonAsciiInputRetainsTheGeneralSemanticRoute()
    {
        const string input = "é LogTrace λLogError LogWarning_ LogDebug.";
        var regex = new Utf8Regex(Pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(
            Regex.Count(input, Pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled),
            regex.Count(bytes));
        // Mixed structural subjects retain owned unit semantics; the ASCII
        // fused route is still qualified by the dedicated ASCII witnesses.
        Assert.Equal("<none>", regex.CollectCountDiagnostics(bytes).ExecutionRoute);
        var expected = Regex.Match(input, Pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var actual = regex.Match(bytes);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(0, expected.Index)), actual.StartOffsetInBytes);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(expected.Index, expected.Length)), (actual.EndOffsetInBytes - actual.StartOffsetInBytes));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Fact]
    public void FiniteTimeoutRetainsTheBudgetedCountRoute()
    {
        // This checks route selection, independently of host scheduling pauses.
        using var clock = Utf8DeadlineTestClock.Start();
        var regex = new Utf8Regex(
            Pattern,
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromSeconds(1));
        var input = "LogTrace LogError"u8.ToArray();

        Assert.False(regex.Inspection.DebugHasFusedAsciiBoundaryLiteralFamilyCount);
        Assert.Equal(2, regex.Count(input));
        Assert.True(clock.Reads > 0);
    }

    [Theory]
    [InlineData(@"\b(?:logTrace|logDebug|logInformation|logWarning)\b")]
    [InlineData(@"\b(?:LogTrace|LogDebug|LogInformation)\b")]
    public void SelectorRejectsUnprovenFamilyShapes(string pattern)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);

        Assert.False(regex.Inspection.DebugHasFusedAsciiBoundaryLiteralFamilyCount);
    }
}
