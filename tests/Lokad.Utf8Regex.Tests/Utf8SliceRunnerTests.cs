using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

/// <summary>R02a slice semantics: hand-lowered concatenation, ordered
/// alternation, greedy/lazy repetition, and captures match the BCL oracle on
/// hits, misses, rollback alternatives, repeated captures, empty matches, and
/// capture values over ASCII input.</summary>
public sealed class Utf8SliceRunnerTests
{
    private static Utf8SliceInvocation Unmetered()
    {
        return new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
    }

    private static Utf8SliceNode Lit(byte value)
    {
        return new Utf8SliceNode.LiteralNode(value);
    }

    private static Utf8SliceNode Lit(string text)
    {
        var children = new Utf8SliceNode[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            children[i] = Lit((byte)text[i]);
        }

        return new Utf8SliceNode.ConcatNode(children);
    }

    private static Utf8SliceNode Set(params (byte Low, byte High)[] ranges)
    {
        var mask = new byte[16];
        foreach (var (low, high) in ranges)
        {
            for (var value = low; value <= high; value++)
            {
                mask[value >> 3] |= (byte)(1 << (value & 7));
            }
        }

        return new Utf8SliceNode.SetNode(mask);
    }

    private static Utf8SliceNode Plus(Utf8SliceNode body)
    {
        return new Utf8SliceNode.RepeatNode(body, 1, int.MaxValue, false);
    }

    private static Utf8SliceNode Star(Utf8SliceNode body, bool lazy = false)
    {
        return new Utf8SliceNode.RepeatNode(body, 0, int.MaxValue, lazy);
    }

    private static Utf8SliceProgram Lower(Utf8SliceNode root)
    {
        return Utf8SliceLowerer.Lower(root);
    }

    private static (int[] Starts, int[] Ends) Slots(Utf8SliceProgram program)
    {
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        return (starts, ends);
    }

    private static void AssertOracleParity(string bclPattern, Utf8SliceProgram program, string inputText)
    {
        var input = Encoding.ASCII.GetBytes(inputText);
        var oracle = new Regex(bclPattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var invocation = Unmetered();
        var (starts, ends) = Slots(program);
        Assert.Equal(oracle.IsMatch(inputText), Utf8SliceRunner.IsMatch(program, input, Utf8BoundaryMap.Create(input), starts, ends, ref invocation).IsMatch);
        invocation = Unmetered();
        (starts, ends) = Slots(program);
        Assert.Equal(oracle.Matches(inputText).Count, Utf8SliceRunner.Count(program, input, Utf8BoundaryMap.Create(input), starts, ends, ref invocation).Count);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData("aaab", true)]
    [InlineData("ab", true)]
    [InlineData("aabbb", true)]
    [InlineData("aaa", false)]
    [InlineData("bbb", false)]
    [InlineData("", false)]
    [InlineData("aabbaa", true)]
    public void ConcatenatedRepeatsMatchOracle(string inputText, bool expected)
    {
        var program = Lower(new Utf8SliceNode.ConcatNode([
            new Utf8SliceNode.CaptureNode(0, Plus(Lit((byte)97))),
            new Utf8SliceNode.CaptureNode(1, Plus(Lit((byte)98))),
        ]));
        var input = Encoding.ASCII.GetBytes(inputText);
        var invocation = Unmetered();
        var (starts, ends) = Slots(program);
        var outcome = Utf8SliceRunner.IsMatch(program, input, Utf8BoundaryMap.Create(input), starts, ends, ref invocation);
        Assert.Equal(expected, outcome.IsMatch);
        Assert.Equal(new Regex(@"(a+)(b+)", RegexOptions.CultureInvariant).IsMatch(inputText), outcome.IsMatch);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void ConcatenatedRepeatsProjectCaptures()
    {
        var program = Lower(new Utf8SliceNode.ConcatNode([
            new Utf8SliceNode.CaptureNode(0, Plus(Lit((byte)97))),
            new Utf8SliceNode.CaptureNode(1, Plus(Lit((byte)98))),
        ]));
        var input = Encoding.ASCII.GetBytes("xxaaabbyy");
        var invocation = Unmetered();
        var (starts, ends) = Slots(program);
        var outcome = Utf8SliceRunner.IsMatch(program, input, Utf8BoundaryMap.Create(input), starts, ends, ref invocation);
        Assert.True(outcome.IsMatch);
        var oracle = new Regex(@"(a+)(b+)", RegexOptions.CultureInvariant).Match("xxaaabbyy");
        Assert.Equal(oracle.Groups[1].Index, starts[0]);
        Assert.Equal(oracle.Groups[1].Index + oracle.Groups[1].Length, ends[0]);
        Assert.Equal(oracle.Groups[2].Index, starts[1]);
        Assert.Equal(oracle.Groups[2].Index + oracle.Groups[2].Length, ends[1]);
    }

    [Theory]
    [InlineData("abab", "ab")]
    [InlineData("cdcdab", "ab")]
    [InlineData("cdcd", "cd")]
    public void RepeatedCaptureKeepsLastIteration(string inputText, string expectedGroup)
    {
        var program = Lower(Plus(new Utf8SliceNode.CaptureNode(0, new Utf8SliceNode.AlternationNode([
            [Lit("ab")],
            [Lit("cd")],
        ]))));
        var input = Encoding.ASCII.GetBytes(inputText);
        var invocation = Unmetered();
        var (starts, ends) = Slots(program);
        var matched = Utf8SliceRunner.TryMatchAt(program, input, 0, starts, ends, ref invocation);
        Assert.True(matched.IsMatch);
        var oracle = new Regex(@"(ab|cd)+", RegexOptions.CultureInvariant).Match(inputText);
        Assert.Equal(oracle.Groups[1].Value, Encoding.ASCII.GetString(input[starts[0]..ends[0]]));
        Assert.Equal(expectedGroup, Encoding.ASCII.GetString(input[starts[0]..ends[0]]));
    }

    [Theory]
    [InlineData("abc", true)]
    [InlineData("ac", true)]
    [InlineData("abbc", false)]
    [InlineData("ab", false)]
    public void RollbackAlternativeMatchesOracle(string inputText, bool expected)
    {
        var program = Lower(new Utf8SliceNode.ConcatNode([
            new Utf8SliceNode.AlternationNode([[Lit((byte)97)], [Lit("ab")]]),
            Lit((byte)99),
        ]));
        AssertOracleParity(@"(a|ab)c", program, inputText);
        var input = Encoding.ASCII.GetBytes(inputText);
        var invocation = Unmetered();
        var (starts, ends) = Slots(program);
        Assert.Equal(expected, Utf8SliceRunner.IsMatch(program, input, Utf8BoundaryMap.Create(input), starts, ends, ref invocation).IsMatch);
    }

    [Theory]
    [InlineData("aaab", true)]
    [InlineData("b", true)]
    [InlineData("aaa", false)]
    public void LazyRepeatMatchesOracle(string inputText, bool expected)
    {
        var program = Lower(new Utf8SliceNode.ConcatNode([
            Star(Lit((byte)97), lazy: true),
            Lit((byte)98),
        ]));
        AssertOracleParity(@"a*?b", program, inputText);
        var input = Encoding.ASCII.GetBytes(inputText);
        var invocation = Unmetered();
        var (starts, ends) = Slots(program);
        Assert.Equal(expected, Utf8SliceRunner.IsMatch(program, input, Utf8BoundaryMap.Create(input), starts, ends, ref invocation).IsMatch);
    }

    [Fact]
    public void AstralInputMatchesOracle()
    {
        var star = Lower(Star(Lit((byte)97)));
        AssertOracleParity("a*", star, "\U0001F600");
        AssertOracleParity("a*", star, "a\U0001F600");
        AssertOracleParity("a*", star, "\U0001F600a");
        var concat = Lower(new Utf8SliceNode.ConcatNode([
            new Utf8SliceNode.CaptureNode(0, Plus(Lit((byte)97))),
            new Utf8SliceNode.CaptureNode(1, Plus(Lit((byte)98))),
        ]));
        AssertOracleParity("(a+)(b+)", concat, "\U0001F600aabb");
        AssertOracleParity("(a+)(b+)", concat, "aa\U0001F600bb");
        AssertOracleParity("(a+)(b+)", concat, "aabb\U0001F600");
        var empty = Lower(new Utf8SliceNode.EmptyNode());
        AssertOracleParity("", empty, "ab");
        AssertOracleParity("", empty, "\U0001F600");
    }

    [Theory]
    [InlineData("b", 2)]
    [InlineData("ab", 3)]
    [InlineData("aab", 3)]
    public void EmptyMatchesAdvanceCount(string inputText, int expectedCount)
    {
        var program = Lower(Star(Lit((byte)97)));
        var input = Encoding.ASCII.GetBytes(inputText);
        var invocation = Unmetered();
        var (starts, ends) = Slots(program);
        var outcome = Utf8SliceRunner.Count(program, input, Utf8BoundaryMap.Create(input), starts, ends, ref invocation);
        Assert.Equal(new Regex("a*", RegexOptions.CultureInvariant).Matches(inputText).Count, outcome.Count);
        Assert.Equal(expectedCount, outcome.Count);
    }

    [Theory]
    [InlineData("b", true, -1, -1, 0, 1)]
    [InlineData("a", true, 0, 1, -1, -1)]
    public void UnmatchedAlternativeLeavesSlotsUnset(string inputText, bool expected, int s0, int e0, int s1, int e1)
    {
        var program = Lower(new Utf8SliceNode.AlternationNode([
            [new Utf8SliceNode.CaptureNode(0, Lit((byte)97))],
            [new Utf8SliceNode.CaptureNode(1, Lit((byte)98))],
        ]));
        var input = Encoding.ASCII.GetBytes(inputText);
        var invocation = Unmetered();
        var (starts, ends) = Slots(program);
        Assert.Equal(expected, Utf8SliceRunner.IsMatch(program, input, Utf8BoundaryMap.Create(input), starts, ends, ref invocation).IsMatch);
        var oracle = new Regex("(a)|(b)", RegexOptions.CultureInvariant).Match(inputText);
        Assert.Equal(oracle.Groups[1].Success, starts[0] >= 0);
        Assert.Equal(oracle.Groups[2].Success, starts[1] >= 0);
        Assert.Equal(s0, starts[0]);
        Assert.Equal(e0, ends[0]);
        Assert.Equal(s1, starts[1]);
        Assert.Equal(e1, ends[1]);
    }

    [Theory]
    [InlineData("xxa@12yy", true)]
    [InlineData("xxabyy", false)]
    [InlineData("123", false)]
    public void CharacterSetsMatchOracle(string inputText, bool expected)
    {
        var program = Lower(new Utf8SliceNode.ConcatNode([
            Plus(Set(((byte)97, (byte)122))),
            Lit((byte)64),
            Plus(Set(((byte)48, (byte)57))),
        ]));
        AssertOracleParity(@"[a-z]+@[0-9]+", program, inputText);
        var input = Encoding.ASCII.GetBytes(inputText);
        var invocation = Unmetered();
        var (starts, ends) = Slots(program);
        Assert.Equal(expected, Utf8SliceRunner.IsMatch(program, input, Utf8BoundaryMap.Create(input), starts, ends, ref invocation).IsMatch);
    }

    [Theory]
    [InlineData("aab", true)]
    [InlineData("aaab", true)]
    [InlineData("aaaab", true)]
    [InlineData("ab", false)]
    [InlineData("a", false)]
    public void BoundedRepeatMatchesOracle(string inputText, bool expected)
    {
        var program = Lower(new Utf8SliceNode.ConcatNode([
            new Utf8SliceNode.RepeatNode(Lit((byte)97), 2, 3, false),
            Lit((byte)98),
        ]));
        AssertOracleParity(@"a{2,3}b", program, inputText);
        var input = Encoding.ASCII.GetBytes(inputText);
        var invocation = Unmetered();
        var (starts, ends) = Slots(program);
        Assert.Equal(expected, Utf8SliceRunner.IsMatch(program, input, Utf8BoundaryMap.Create(input), starts, ends, ref invocation).IsMatch);
    }
}
