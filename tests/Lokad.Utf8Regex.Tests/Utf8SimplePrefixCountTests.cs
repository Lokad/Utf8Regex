using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SimplePrefixCountTests
{
    [Theory]
    [InlineData(@"(?<head>[\w.+-]+@)[\w-]+\.[\w.-]+", "bad@? 😀éclair@chez.fr xx@z.rs")]
    [InlineData(@"([\w.+-]+)@([\w-]+)\.([\w.-]+)", "bad@? 😀éclair@chez.fr xx@z.rs")]
    [InlineData(@"(?<key>\w+): (?<value>\w+)", "éclair: thé 😀name: café")]
    [InlineData(@"(?<run>[\w@]{1,4})@(?<end>[!?])", "ab@cd@! é@@? 😀x@@!")]
    [InlineData(@"(?<w>\w+)@\k<w>", "bad@? ab@b 😀éé@é")]
    [InlineData(@"[^;]+;[A-Z]", "bad;? 😀é;Z xy;A")]
    [InlineData(@"[\w,]{2,4},\d", "a,none! 😀éxyz,5 bc,7")]
    [InlineData(@"(?<=(?<lead>é))(?<word>[A-Z][a-z]+)(?=\s|$)", "abcdéAlpha 😀éBeta")]
    [InlineData(@"(?=(?<first>[A-Z]))[A-Z](?<!Z)[a-z]+", "Zed  😀Alpha Zulu Beta")]
    [InlineData(@"(?![A-Z]x)[A-Z](?<!Z)[a-z]+", "Ax Zed 😀Bravo")]
    public void NecessaryStartsKeepFirstMatchAndByteFloor(string pattern, string tail)
    {
        var text = new string(' ', 300) + tail;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        using var cancellation = new CancellationTokenSource();
        using var regex = new Utf8Regex(pattern, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 100_000, MaxWorkingBytes = 1 << 20 }
        });
        Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input, cancellation.Token));
        Assert.Equal(oracle.Count(text), regex.Count(input, cancellation.Token));
        foreach (var start in new[] { 0, 305 })
        {
            var byteFloor = Encoding.UTF8.GetByteCount(text.AsSpan(0, start));
            var expected = oracle.Match(text, start);
            var cursor = Utf8MatchSequence.First(regex.CreateMatchCursor(input, byteFloor, cancellation.Token));
            Utf8MatchSequence.AssertCaptures(expected, cursor, input, text);
            var expectedRanges = oracle.Matches(text, start).Select(match =>
                (Encoding.UTF8.GetByteCount(text.AsSpan(0, match.Index)),
                 Encoding.UTF8.GetByteCount(text.AsSpan(0, match.Index + match.Length))));
            var actualRanges = new List<(int, int)>();
            foreach (var match in regex.EnumerateMatches(input, byteFloor, cancellation.Token))
                actualRanges.Add((match.StartOffsetInBytes, match.EndOffsetInBytes));
            Assert.Equal(expectedRanges, actualRanges);
        }
    }

    [Theory]
    [InlineData("((?:AB|XY)([0-9]{2}))")]
    [InlineData("(ab|a)[0-9]{2}")]
    [InlineData("(?:aba|ba)[0-9]{2}")]
    [InlineData("prefix[0-9]")]
    [InlineData("[0-9]longsuffix")]
    [InlineData("(?i:ab)[0-9]{2}")]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+")]
    [InlineData(@"\b(?:record|rec|class)\s+[A-Z][A-Za-z0-9_]+")]
    [InlineData(@"(?<head>#)(?<text>.+)\k<head>")]
    [InlineData(@"(?:# [Nn][Oo][Qq][Aa])(?::\s?(([A-Z]+[0-9]+(?:[,\s]+)?)+))?")]
    public void PrefixCandidatesPreserveVerificationOrderOffsetsAndTails(string pattern)
    {
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        foreach (var compiled in new[] { false, true })
        {
            var regex = new Utf8Regex(pattern, options: Options(compiled));
            foreach (var text in new[] { "", "AB", "ABxXY12", "aba12 ab34 a56", "AB12XY34ABxxAB56",
                "😀ab12é AB34", "prefix1prefixxprefix2", "1longsuffix 2longsuffix", "aB12 AB34",
                "éclass Foo 😀class Bar record Baz struct lower class Last",
                "class\u00a0Name record\u2003Other xrecord Nope rec Short",
                "é#😀word #line# # noqa: F401,E123 # NOQA #noqa # nearTail",
                "😀" + new string('x', 2042) + "AB12XY34aba56 class NearTail" })
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                foreach (var offset in new[] { 0, 3, 9 })
                {
                    var storage = new byte[offset + bytes.Length + 7];
                    bytes.CopyTo(storage, offset);
                    Assert.Equal(oracle.Count(text), regex.Count(storage.AsSpan(offset, bytes.Length)));
                }
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SafePrefixesAndDependentCapturesRespectWorkingAdmission(bool compiled)
    {
        const string pattern = "((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))";
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxDepth = 1, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 };
        var regex = new Utf8Regex(pattern, options: Options(compiled), policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = policy
        });
        // Necessary prefixes skip misses in valid mixed text. Complete hits
        // use the general program's admitted workspace.
        var input = Encoding.UTF8.GetBytes("😀" + new string('x', 65536));
        using var token = new CancellationTokenSource();
        Assert.Equal(0, regex.Count(input, token.Token));
        Assert.Equal(1, regex.Count("😀ASIAABCDEFGHIJKLMNOP"u8, token.Token));
        // A backreference still depends on captured values and retains admission
        // for its real scalar workspace under the same small allowance.
        var dependent = new Utf8Regex(pattern + @"\1", options: Options(compiled), policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = policy with
            {
                MaxWorkingBytes = 256
            }
        });
        Assert.Equal(1, new Utf8Regex(pattern + @"\1", options: Options(compiled)).Count("😀ASIAABCDEFGHIJKLMNOPASIAABCDEFGHIJKLMNOP"u8));
        Assert.Throws<Utf8ResourceLimitExceededException>(() => dependent.Count("😀ASIAABCDEFGHIJKLMNOPASIAABCDEFGHIJKLMNOP"u8, token.Token));
        var denied = new Utf8Regex(pattern, options: Options(compiled), policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = policy with
            {
                MaxWorkSteps = 1
            }
        });
        Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Count(input));
        token.Cancel();
        var canceled = Assert.Throws<OperationCanceledException>(() => regex.Count(input, token.Token));
        Assert.Equal(token.Token, canceled.CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrefixProofDoesNotAcceptCandidatesOrChangeRetainedCaptures(bool compiled)
    {
        var regex = new Utf8Regex("((?:AB|XY)([0-9]{2}))", options: Options(compiled));
        var input = "😀ABxxXY12 AB34 XY"u8.ToArray();
        Assert.Equal(2, regex.Count(input));
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.Equal("XY12", System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span));
        Assert.Equal("12", System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(2).Span));
        Assert.Equal(2, regex.Count(input));
        Assert.Equal("12", System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(2).Span));
    }

    [Fact]
    public void PublicScalarCountUsesPrefixProofUnderFiniteLimits()
    {
        const string pattern = @"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+";
        var input = Encoding.UTF8.GetBytes("😀" + new string('x', 65536));
        using var regex = Create(1000000);
        Assert.Equal(0, regex.Count(input));
        // Unicode word boundaries, whitespace and the suffix still require verification.
        Assert.Equal(2, regex.Count("éclass Wrong 😀class\u00a0Right record Good"u8));
        using var denied = Create(1);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Count(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);

        static Utf8Regex Create(ulong steps) => new(pattern, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = steps, MaxDepth = 32, MaxWorkingBytes = 2048 }
        });
    }

    [Fact]
    public void LeadingAsciiLiteralKeepsScalarDotAndDeepCaptureSemantics()
    {
        // A supplementary scalar is one dot; the UTF-16 BCL matcher is not
        // an oracle for this case.
        foreach (var compiled in new[] { false, true })
        {
            var regex = new Utf8Regex("#.[a-z]+", options: Options(compiled));
            Assert.Equal(2, regex.Count("#😀word #xfoo"u8));
        }
        var deep = new Utf8Regex(new string('(', 100) + "#x" + new string(')', 100));
        var input = "zz#x zz#x"u8.ToArray();
        var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(1000000, 1024, 1 << 20),
            Regex.InfiniteMatchTimeout, default);
        var program = deep.Inspection.AvailableOwnedSliceProgram;
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var result = Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref invocation);
        Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
        Assert.Equal(2, result.Count);
    }

    private static RegexOptions Options(bool compiled) => RegexOptions.CultureInvariant |
        (compiled ? RegexOptions.Compiled : RegexOptions.None);
}
