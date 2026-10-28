using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SimplePrefixCountTests
{
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
            var regex = new Utf8Regex(pattern, Options(compiled));
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
    public void AnAbsentExactPrefixNeedsNoVmWorkspaceButHitsStillRequireAdmission(bool compiled)
    {
        const string pattern = "((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))";
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxDepth = 1,
            MaxWorkingBytes = 256, MaxResultBytes = 1 };
        var regex = new Utf8Regex(pattern, Options(compiled), Regex.InfiniteMatchTimeout, policy);
        // The non-ASCII scalar selects the owned scalar route. A prefix miss
        // can finish with only bounded capture slots, without acquiring VM
        // frames; a surviving candidate still requires those major buffers.
        var input = Encoding.UTF8.GetBytes("😀" + new string('x', 65536));
        using var token = new CancellationTokenSource();
        Assert.Equal(0, regex.Count(input, token.Token));
        Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Count("😀ASIAABCDEFGHIJKLMNOP"u8, token.Token));

        byte[] malformed = [.. input, 0xF0, 0x80];
        Assert.Throws<ArgumentException>(() => regex.Count(malformed, token.Token));
        var denied = new Utf8Regex(pattern, Options(compiled), Regex.InfiniteMatchTimeout,
            policy with { MaxWorkSteps = 1 });
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
        var regex = new Utf8Regex("((?:AB|XY)([0-9]{2}))", Options(compiled));
        var input = "😀ABxxXY12 AB34 XY"u8.ToArray();
        Assert.Equal(2, regex.Count(input));
        var match = regex.MatchDetailed(input);
        Assert.Equal("XY12", match.GetValueString());
        Assert.Equal("12", match.GetGroup(2).GetValueString());
        Assert.Equal(2, regex.Count(input));
        Assert.Equal("12", match.GetGroup(2).GetValueString());
    }

    [Fact]
    public void PreparedScalarCountBorrowsPrefixProofAndOriginalWorkingAuthority()
    {
        var regex = new Utf8Regex(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+");
        var input = Encoding.UTF8.GetBytes("😀" + new string('x', 65536));
        var validation = Utf8Validation.Validate(input);
        var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(1000000, 1, 256),
            Regex.InfiniteMatchTimeout, default);
        var breach = Utf8PreparedScalarExecutor.TryCount(regex.Inspection.PreparedRegex,
            input, validation, ref invocation, out var count);
        Assert.Equal(Utf8ExecutionLimitKind.None, breach);
        Assert.Equal(0, count);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);

        // A later native-looking prefix is not sufficient: Unicode word
        // boundaries, Unicode whitespace and the whole suffix stay scalar.
        Assert.Equal(2, regex.Count("éclass Wrong 😀class\u00a0Right record Good"u8));
        var denied = new Utf8SliceInvocation(new Utf8ExecutionLimits(1, 1, 256),
            Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.WorkSteps,
            Utf8PreparedScalarExecutor.TryCount(regex.Inspection.PreparedRegex,
                input, validation, ref denied, out _));
    }

    [Fact]
    public void LeadingAsciiLiteralKeepsScalarDotAndDeepCaptureSemantics()
    {
        // A supplementary scalar is one dot; the UTF-16 BCL matcher is not
        // an oracle for this case.
        foreach (var compiled in new[] { false, true })
        {
            var regex = new Utf8Regex("#.[a-z]+", Options(compiled));
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
