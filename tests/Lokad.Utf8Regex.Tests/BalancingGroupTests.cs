using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class BalancingGroupTests
{
    private static string Pattern(string begin, string end) =>
        $@"{Regex.Escape(begin)}(?:(?<open>{Regex.Escape(begin)})|(?<-open>{Regex.Escape(end)})|(?:(?!{Regex.Escape(begin)}|{Regex.Escape(end)})[\s\S]))*{Regex.Escape(end)}(?(open)(?!))";

    [Theory]
    [InlineData("BEGIN", "END")]
    [InlineData("START", "STOP")]
    [InlineData("début", "終")]
    public void ExhaustiveTokenSequencesMatchTheScalarCompatibleOracle(string begin, string end)
    {
        var pattern = Pattern(begin, end);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var oracle = new Regex(pattern, options);
            var regex = new Utf8Regex(pattern, options);
            for (var length = 0; length <= 8; length++)
            for (var combination = 0; combination < 1 << length; combination++)
            {
                var text = string.Concat(Enumerable.Range(0, length)
                    .Select(index => ((combination & (1 << index)) == 0 ? begin : end) + "é"));
                Assert.Equal(oracle.Count(text), regex.Count(Encoding.UTF8.GetBytes(text)));
            }
        }
    }

    [Theory]
    [InlineData("BEGIN", "END")]
    [InlineData("START", "STOP")]
    [InlineData("début", "終")]
    public void PublicOperationsAgreeAcrossControlsAndUnmatchedTokens(string begin, string end)
    {
        var pattern = Pattern(begin, end);
        string[] subjects = ["", begin + end, begin + begin + end + end, begin + begin + end,
            begin + end + end, end + begin + end, begin + end + begin + end,
            "é😀 " + begin + "outer " + begin + "inner" + end + "tail" + end + " 終",
            begin + " outer " + begin + "complete" + end + " incomplete " + begin,
            begin + "😀\0\n" + end];
        using var cancellation = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        foreach (var finite in new[] { false, true })
        {
            var regex = new Utf8Regex(pattern, options, matchTimeout: finite ? TimeSpan.FromSeconds(10) : null,
                resourcePolicy: finite ? new() { MaxWorkSteps = 1_000_000, MaxDepth = 4096,
                    MaxWorkingBytes = 16 << 20, MaxResultBytes = 16 << 20 } : default);
            var oracle = new Regex(pattern, options);
            foreach (var text in subjects)
            {
                var input = Encoding.UTF8.GetBytes(text);
                var expected = oracle.Matches(text);
                Assert.Equal(expected.Count, regex.Count(input));
                Assert.Equal(expected.Count, regex.Count(input, cancellation.Token));
                Assert.Equal(expected.Count != 0, regex.IsMatch(input, cancellation.Token));
                using var cursor = regex.CreateMatchCursor(input, cancellationToken: cancellation.Token);
                foreach (Match match in expected)
                {
                    Assert.True(cursor.MoveNext(out var actual));
                    Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, match.Index)), actual.Value.StartOffsetInBytes);
                    Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, match.Index + match.Length)), actual.Value.EndOffsetInBytes);
                    Assert.True(actual.TryGetGroup("open", out var stack));
                    Assert.Equal(match.Groups["open"].Success, stack.Success);
                }
                Assert.False(cursor.MoveNext(out _));
            }
        }
    }

    [Fact]
    public void CaptureIdentityAliasesAndChangedGrammarsKeepTheirOwnSemantics()
    {
        var original = Pattern("BEGIN", "END");
        var alias = original.Replace("open", "stack").Replace("BEGIN", @"(?:\x42)EGIN").Replace("END", @"E(?:\x4e)D");
        foreach (var pattern in new[] { original, alias,
            original.Replace("(?!BEGIN|END)", "(?!BEGIN)"), original.Replace("[\\s\\S]", "."),
            original.Replace("*END", "*?END"), original.Replace("(?(open)(?!))", ""), @"(?<open>x?)" + original })
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.IgnoreCase })
        {
            var regex = new Utf8Regex(pattern, options);
            var oracle = new Regex(pattern, options);
            foreach (var text in new[] { "BEGINEND", "BEGIN😀BEGINéEND tail END BEGINxEND", "BEGINBEGIN😀END", "beginEND", "BEGIN\nEND" })
            {
                Assert.Equal(oracle.Matches(text).Count, regex.Count(Encoding.UTF8.GetBytes(text)));
            }
        }
        var retained = Utf8MatchSequence.First(new Utf8Regex(alias).CreateMatchCursor("BEGIN😀END"u8.ToArray()));
        Assert.True(retained.Success);
        Assert.False((retained.TryGetGroup("stack", out var range1) ? range1.Success : default));
    }

    [Theory]
    [InlineData("BEGINEND", 1)]
    [InlineData("BEGIN😀BEGINéEND tail END BEGINxEND", 1)]
    [InlineData("BEGINBEGIN😀END", 1)]
    [InlineData("beginEND", 0)]
    [InlineData("BEGIN\nEND", 1)]
    public void RightToLeftSelfBalancingUsesStableSemanticOutcomes(string text, int expected)
    {
        // .NET 10 can throw in TransferCapture for this RTL self-balancing group,
        // including full MatchCollection matching. These outcomes were checked
        // against .NET 11; the owned engine does not reproduce the .NET 10 crash.
        var pattern = Pattern("BEGIN", "END").Replace("(?<-open>END)", "(?<open-open>END)");
        foreach (var options in new[] { RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
            Assert.Equal(expected, new Utf8Regex(pattern, options).Count(Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public void GeneralEnginePreservesDeepNestingAndRetainedCaptures()
    {
        var pattern = Pattern("BEGIN", "END");
        var deep = string.Concat(Enumerable.Repeat("BEGIN", 129)) + "😀" + string.Concat(Enumerable.Repeat("END", 129));
        var regex = new Utf8Regex(pattern);
        Assert.Equal(1, regex.Count(Encoding.UTF8.GetBytes(deep)));
        var input = "BEGIN😀BEGINéEND tail END BEGINxEND"u8.ToArray();
        var retained = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.Equal("BEGIN😀BEGINéEND tail END", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        Assert.Equal(2, regex.Count(input));
        Assert.Equal("BEGIN😀BEGINéEND tail END", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        Assert.False((retained.TryGetGroup("open", out var range2) ? range2.Success : default));
    }

    [Fact]
    public void UsefulLimitsAndOriginalTokenStillApplyToGeneralExecution()
    {
        var pattern = Pattern("BEGIN", "END");
        var input = "BEGINBEGIN😀ENDEND"u8.ToArray();
        foreach (var policy in new[] { new Utf8ResourcePolicy { MaxWorkSteps = 1 }, new() { MaxDepth = 1 }, new() { MaxWorkingBytes = 1 } })
        {
            var regex = new Utf8Regex(pattern, resourcePolicy: policy, inputValidation: Utf8InputValidation.AssumeValid);
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Count(input));
        }
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var owner = new Utf8Regex(pattern);
        Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() => owner.Count(input, cancellation.Token)).CancellationToken);
    }
}
