using System.Text;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CapturedReplacementIterationTests
{
    [Theory]
    [InlineData("foo(?<Bar>BAR)?", "αfooBAR😀fooβ", "α[2:6]😀[12:3]β")]
    [InlineData("(a)(?1)", "aaaa", "[0:2][2:2]")]
    [InlineData("(*MARK:tag)(?<=abc)(|def)", "abcdefabc😀", "abc[3:0][3:3]abc[9:0]😀")]
    [InlineData(@"(a\Kb)*", "ab xx ab yy ab", "a[1:1][2:0] [3:0]x[4:0]x[5:0] a[7:1][8:0] [9:0]y[10:0]y[11:0] a[13:1][14:0]")]
    [InlineData(@"(?:a\Kb)*", "ab xx ab", "a[1:1][2:0] [3:0]x[4:0]x[5:0] a[7:1][8:0]")]
    public void CapturedReplacementIterationPreservesGrammarMarkAndResetEmptyYields(string pattern, string input, string expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 1 << 20
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var compiled = regex.DebugCompiledProgram;
        var program = Assert.IsType<Pcre2BacktrackingDirectProgram>(compiled.Operations.Replace).Program;
        foreach (var options in new[] { Pcre2MatchOptions.None, Pcre2MatchOptions.NotBol })
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            ReadOnlySpan<byte> subject = bytes;
            var invocation = Pcre2SliceDriver.CreateInvocation(compiled.Request, CancellationToken.None);
            var cursor = new Pcre2BacktrackingDetailedGlobalMatchCursor(program, compiled.CandidateSearchPlan,
                subject, new(0), options, compiled.Request,
                new Pcre2ResourceBudget(compiled.Request.DefaultLimits, invocation, collectDiagnostics: false));
            var output = new StringBuilder();
            var calls = new List<(int, int, string)>();
            var position = 0;
            while (cursor.MoveNext())
            {
                var groups = cursor.ProjectCurrentCaptures();
                var value = groups[0];
                calls.Add((value.StartOffsetInBytes, value.EndOffsetInBytes, cursor.Current.Mark ?? ""));
                output.Append(Encoding.UTF8.GetString(bytes.AsSpan(position, value.StartOffsetInBytes - position)));
                output.Append($"[{value.StartOffsetInBytes}:{value.EndOffsetInBytes - value.StartOffsetInBytes}]");
                position = value.EndOffsetInBytes;
            }
            output.Append(Encoding.UTF8.GetString(bytes.AsSpan(position)));
            Assert.Equal(expected, output.ToString());
            if (pattern.StartsWith("(*MARK", StringComparison.Ordinal))
                Assert.Equal([(3, 3, "tag"), (3, 6, "tag"), (9, 9, "tag")], calls);
        }
    }
}
