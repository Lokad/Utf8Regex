using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class ScalarBoundaryLiteralFamilyCountTests
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
            var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                ResourcePolicy = controlled ? new()
                {
                    MaxWorkSteps = 1_000_000,
                    MaxDepth = 1024,
                    MaxWorkingBytes = 1 << 20,
                    MaxResultBytes = 1 << 20
                }

                : default
            });
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
    public void CompiledFamilyConfirmsScalarBoundariesAtLiteralCandidates()
    {
        using var regex = new Utf8Regex(Pattern, options: RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var input = "𐐀LogTrace LogDebug𐐀 \u200cLogInformation 😀LogError😀 LogWarning"u8;
        Assert.Equal(2, regex.Count(input));

        if (regex.Inspection.HasEmittedLiteralFamily)
        {
            Assert.Equal(
                "compiled_scalar_boundary_literal_family_count",
                regex.CollectCountDiagnostics(input).ExecutionRoute);
        }
    }

    [Fact]
    public void CountHandlesBorrowedOffsetsAndTailPositions()
    {
        var regex = new Utf8Regex(Pattern, options: RegexOptions.CultureInvariant | RegexOptions.Compiled);
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
    public void CountMatchesDotNetOnDeterministicAsciiSubjects()
    {
        var regex = new Utf8Regex(Pattern, options: RegexOptions.CultureInvariant | RegexOptions.Compiled);
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
    public void FiniteTimeoutRetainsTheBudgetedCountRoute()
    {
        // This checks route selection, independently of host scheduling pauses.
        using var clock = Utf8DeadlineTestClock.Start();
        using var regex = new Utf8Regex(Pattern, options: RegexOptions.CultureInvariant | RegexOptions.Compiled, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromSeconds(1)
        });
        var input = "LogTrace LogError"u8.ToArray();

        Assert.Equal(2, regex.Count(input));
        Assert.True(clock.Reads > 0);
    }

    [Fact]
    public void EcmaScriptUsesItsOwnWordMembership()
    {
        const RegexOptions options = RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.ECMAScript;
        const string input = "éLogTrace LogDebugé 😀LogError😀";
        using var regex = new Utf8Regex(Pattern, options);
        Assert.Equal(Regex.Count(input, Pattern, options), regex.Count(Encoding.UTF8.GetBytes(input)));
    }
}
