using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8TokenMaskPreparationTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static PreparedAsciiDelimitedTokenSearch Delimited() => new("::"u8, "."u8, "ab"u8, "xy"u8, "12"u8);
    private static PreparedAsciiLiteralStructuredTokenSearch Structured() => new(":"u8, "ab"u8, "ab"u8, "ab12"u8, [0], "x"u8, [], []);

    [Fact]
    public void FinderAndPreparedMembershipRetainOnlyTheirOwnedLiteralBytes()
    {
        foreach (var kind in new[] { 0, 1, 2 })
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
            if (kind == 0)
            {
                var bytes = "abcdefghij"u8.ToArray(); var finder = new Utf8AsciiLiteralFinder(bytes); bytes[0] = (byte)'z';
                Assert.Equal(2, finder.IndexOf("xxabcdefghij"u8));
                Assert.Equal(Utf8ConstructionBudget.ArrayBytes<byte>(10), owner.Budget.ReservedBytes);
            }
            else if (kind == 1) { Assert.True(Delimited().HasValue); Assert.Equal(Utf8ConstructionBudget.ArrayBytes<byte>(2), owner.Budget.ReservedBytes); }
            else { Assert.True(Structured().HasValue); Assert.Equal(Utf8ConstructionBudget.ArrayBytes<byte>(1), owner.Budget.ReservedBytes); }
            Assert.Equal(owner.Budget.ReservedBytes, owner.Budget.PeakBytes);
            Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved); Assert.True(owner.Budget.WorkSteps > 0);
        }
        foreach (var type in new[] { typeof(PreparedAsciiDelimitedTokenSearch), typeof(PreparedAsciiLiteralStructuredTokenSearch) })
            Assert.DoesNotContain(type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public), field => field.FieldType.FullName?.Contains("SearchValues", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void EveryByteAndWorkCutoffTransfersACompletePreparedTokenOrRestoresParentLoans()
    {
        foreach (var structured in new[] { false, true })
        {
            void Build() { if (structured) Assert.True(Structured().HasValue); else Assert.True(Delimited().HasValue); }
            ulong peak, work;
            using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
            { Build(); peak = funded.Budget.PeakBytes; work = funded.Budget.WorkSteps; }
            Assert.InRange(peak, 1ul, 200ul); Assert.InRange(work, 1ul, 200ul);
            foreach (var bytes in new[] { true, false })
            {
                var limit = bytes ? peak : work; var successes = 0;
                for (var cutoff = 0ul; cutoff <= limit; cutoff++)
                {
                    using var owner = new Utf8ConstructionScope(0, bytes ? 128 + cutoff : ulong.MaxValue, bytes ? ulong.MaxValue : 2 + cutoff, ulong.MaxValue);
                    var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
                    var failure = Record.Exception(Build);
                    if (failure is null) { successes++; Assert.Equal(128 + peak, owner.Budget.ReservedBytes); }
                    else
                    {
                        Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps,
                            Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                        Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
                    }
                    Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
                    Assert.True(bytes ? owner.Budget.PeakBytes <= 128 + cutoff : owner.Budget.WorkSteps <= 2 + cutoff);
                }
                Assert.Equal(1, successes);
            }
        }
    }

    [Fact]
    public void TokenRootsAdmitDepthAndRejectNonAsciiDefinitionsBeforeAcquiringLiteralStorage()
    {
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 2);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
        owner.Budget.EnterDepth(); owner.Budget.EnterDepth();
        try
        {
            foreach (var build in new Action[] { () => Delimited(), () => Structured(), () => new Utf8AsciiLiteralFinder("a"u8) })
            {
                var failure = Assert.Throws<Utf8ResourceLimitExceededException>(build); Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
                Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes); Assert.Equal(2ul, owner.Budget.Depth);
            }
        }
        finally { owner.Budget.LeaveDepth(); owner.Budget.LeaveDepth(); }
        Assert.Throws<ArgumentOutOfRangeException>(() => new PreparedAsciiDelimitedTokenSearch(":"u8, "."u8, [0xff], "ab"u8, "xy"u8));
        Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.True(Delimited().HasValue); Assert.Equal(128ul + 48ul, owner.Budget.ReservedBytes); Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth);
    }

    [Fact]
    public void ScalarNulIsARealOptionalDelimiterAndEmptyTailStillConsumesItsDelimiter()
    {
        var input = "😀 a:ab\0xx Ω"u8;
        var search = Structured(); Assert.True(search.TryFindNext(input, 0, out var index, out var length));
        Assert.Equal((5, 7), (index, length)); Assert.Equal(1, search.Count(input));
        Assert.True(Utf8AsciiLiteralStructuredTokenExecutor.TryFindStructuredToken(input, 0, ":"u8, "ab"u8, "ab"u8, "ab12"u8, [0], "x"u8, [], [], out index, out length));
        Assert.Equal((5, 7), (index, length));
        var emptyTail = new PreparedAsciiLiteralStructuredTokenSearch(":"u8, "ab"u8, "ab"u8, "ab12"u8, [0], [], [], []);
        Assert.True(emptyTail.TryFindNext(input, 0, out index, out length)); Assert.Equal((5, 5), (index, length));
        Assert.False(search.TryFindNext(input, 6, out _, out _));
    }

    [Fact]
    public void FirstAndSecondBodyRunsPreserveGreedyScalarRegexOutcomes()
    {
        foreach (var first in new[] { "a", "b", "ab" })
        foreach (var second in new[] { "a", "b", "ab" })
        {
            var pattern = "[a]+:[" + first + "]+[" + second + "]+";
            var search = new PreparedAsciiLiteralStructuredTokenSearch(":"u8, "a"u8, Encoding.UTF8.GetBytes(first), Encoding.UTF8.GetBytes(second), [], [], [], []);
            var oracle = new Regex(pattern);
            foreach (var text in Subjects(5))
            {
                var input = Encoding.UTF8.GetBytes(text); var expected = oracle.Match(text);
                Assert.Equal(oracle.Count(text), search.Count(input)); Assert.Equal(expected.Success, search.TryFindNext(input, 0, out var index, out var length));
                if (expected.Success) Assert.Equal((expected.Index, expected.Length), (index, length));
            }
        }
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex("[a-z]+:[a-z]+[0-9]+", options);
            var input = "😀 a:abc12 é"u8;
            Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
            var match = regex.Match(input); var detailed = regex.MatchDetailed(input);
            Assert.Equal((5, 7), (match.IndexInBytes, match.LengthInBytes)); Assert.Equal((5, 7), (detailed.IndexInBytes, detailed.LengthInBytes));
            Assert.Equal(1, regex.Count("a:abc12"u8));
            Assert.Equal(7, regex.Match("a:abc12"u8).LengthInBytes);
        }
    }

    private static IEnumerable<string> Subjects(int remaining, string prefix)
    {
        yield return prefix;
        if (remaining == 0) yield break;
        foreach (var value in "ab:") foreach (var text in Subjects(remaining - 1, prefix + value)) yield return text;
    }
    private static IEnumerable<string> Subjects(int maximum) => Subjects(maximum, "");

    [Fact]
    public void RawEnumerationPreparesOneLiteralCopyAndUsesTheSharedPreparedMatcher()
    {
        foreach (var structured in new[] { false, true })
        {
            using var owner = new Utf8ConstructionScope(0, 48, ulong.MaxValue, ulong.MaxValue);
            for (var repeat = 0; repeat < 20; repeat++)
            {
                var count = structured
                    ? Utf8AsciiLiteralStructuredTokenExecutor.CountStructuredTokens("a:ab b:ab"u8, ":"u8, "ab"u8, "ab"u8, "ab12"u8, [], [], [], [])
                    : Utf8AsciiDelimitedTokenExecutor.CountDelimitedTokens("a::xy.12 b::xy.12"u8, "::"u8, "."u8, "ab"u8, "xy"u8, "12"u8);
                Assert.Equal(2, count); Assert.Equal(0ul, owner.Budget.ReservedBytes);
            }
            Assert.Equal(Utf8ConstructionBudget.ArrayBytes<byte>(structured ? 1 : 2), owner.Budget.PeakBytes);
            Assert.Equal(0ul, owner.Budget.Depth); Assert.True(owner.Budget.WorkSteps > 0);
        }
    }
}
