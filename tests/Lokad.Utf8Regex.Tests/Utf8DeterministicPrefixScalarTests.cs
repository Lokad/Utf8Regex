using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;
using Xunit;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8DeterministicPrefixScalarTests
{
    private static object? Call(string name, params object?[] arguments)
    {
        var method = typeof(Utf8FrontEndSearchAnalyzer).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        try { return method.Invoke(null, arguments); }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        { ExceptionDispatchInfo.Capture(inner).Throw(); throw; }
    }

    private static Utf8FixedDistanceSet[] Extract(Utf8TreeNode node, int startIndex = 0)
    {
        object?[] arguments = [node, startIndex, null];
        var success = Assert.IsType<bool>(Call("TryExtractDeterministicPrefixSets", arguments));
        var sets = Assert.IsType<Utf8FixedDistanceSet[]>(arguments[2]);
        Assert.Equal(sets.Length != 0, success);
        return sets;
    }

    [Fact]
    public void AnUnknownNestedPrefixStopsAllFollowingDistanceFacts()
    {
        foreach (var pattern in new[] { "(ab.)cd", "(ab.*)cd", "(ab[é😀])cd", "(ab(?i:k))cd", "(ab\\1)cd" })
        {
            var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
            // A bare reference needs its group; the parser above still has group 1.
            Assert.NotNull(tree);
            var sets = Extract(tree.Root);
            Assert.Equal([0, 1], sets.Select(set => set.Distance));
            Assert.True(sets[0].Contains((byte)'a'));
            Assert.True(sets[1].Contains((byte)'b'));
        }
    }

    [Fact]
    public void RawFoldedLiteralFactsCoverTheirWholeAsciiCaseFiber()
    {
        var folded = Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "abc");
        var sets = Extract(folded);
        Assert.Equal(3, sets.Length);
        for (var index = 0; index < sets.Length; index++)
        {
            Assert.Equal(index, sets[index].Distance);
            Assert.True(sets[index].Contains((byte)(index + 'a')));
            Assert.True(sets[index].Contains((byte)(index + 'A')));
        }
        var kelvin = Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "abkcd");
        Assert.Equal([0, 1], Extract(kelvin).Select(set => set.Distance));
        var cultureSensitive = Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.IgnoreCase, "abicd");
        Assert.Equal([0, 1], Extract(cultureSensitive).Select(set => set.Distance));
    }

    [Fact]
    public void PrefixMaskLimitIsEnforcedBeforeAppending()
    {
        var node = Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.CultureInvariant, new string('a', 100));
        var sets = Extract(node);
        Assert.Equal(32, sets.Length);
        Assert.Equal(Enumerable.Range(0, 32), sets.Select(set => set.Distance));
    }

    [Fact]
    public void FixedScalarWidthsKeepByteDistancesAndLargeScoresDoNotOverflow()
    {
        foreach (var rtl in new[] { RegexOptions.None, RegexOptions.RightToLeft })
        foreach (var row in new (string Pattern, int[] Distances)[]
        {
            ("(ab😀)cd", [0, 1, 6, 7]),
            ("a[é-ê]{2}bc", [0, 5, 6]),
            ("(ab[😀-🙏])cd", [0, 1, 6, 7]),
            ("a(?=é)bc", [0, 1, 2]),
            ("abc{2147483600}de", [0, 1, 2147483602, 2147483603]),
        })
        {
            var tree = Utf8OwnedTreeParser.TryParse(row.Pattern, RegexOptions.CultureInvariant | rtl);
            Assert.NotNull(tree);
            Assert.Equal(row.Distances, Extract(tree.Root).Select(set => set.Distance));
        }
        var malformed = Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.CultureInvariant, "ab\ud800cd");
        Assert.Equal([0, 1], Extract(malformed).Select(set => set.Distance));
        var ascii = Utf8FrontEndSearchAnalyzer.Analyze(Utf8FrontEnd.Analyze("[ \\t\\r\\n\\f\\v]*abc[0-9]", RegexOptions.CultureInvariant).SemanticRegex);
        Assert.Equal(Utf8FallbackStartTransformKind.TrimLeadingAsciiWhitespace, ascii.FallbackStartTransform.Kind);
        Assert.Equal(Utf8SearchKind.FixedDistanceAsciiSets, ascii.Kind);
        Assert.NotEmpty(ascii.FixedDistanceSets);
        var far = Utf8FixedDistanceSet.FromMask(int.MaxValue, AsciiCharClass.ForByte((byte)'c'));
        Assert.True(Assert.IsType<long>(Call("ScoreDeterministicPrimarySet", far)) > int.MaxValue);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, 127, ulong.MaxValue);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Call("ScoreDeterministicPrimarySet", far));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.Equal(127ul, owner.Budget.WorkSteps);
    }

    [Fact]
    public void EveryPrefixConstraintCoversIndependentScalarVmMatchesAndPublicLanes()
    {
        var patterns = new[] { "(ab.)cd", "(ab.*)cd", "(ab[é😀])cd", "(ab(?i:k))cd", "(ab😀)cd",
            "a[é-ê]{2}bc", "a(?=é)ébc", "a(?i:b)cd", "(?i:abc)de", "[A-Z][0-9]xy",
            "\\s*abc[0-9]", "[ \\t\\r\\n\\f\\v]*abc[0-9]", "\\A(ab.)cd\\z", "(ab|ac)de", "(?i:ki)ab", "(?i:ab)cd", "a(?>b.)cd" };
        var subjects = new[] { "ab😀cd", "abxcd", "abécd", "abKcd", "abkcd", "abKcdab😀cd", "xab😀cdy",
            "aéêbc", "aêébc", "aébc", "aBcd", "ABCDE", "aBcde", "A1xy", "  abc7", "\u00a0abc7", "abde", "acde", "KIab", "ABcd" }
            .Select(Encoding.UTF8.GetBytes).ToArray();
        var observed = 0;
        foreach (var pattern in patterns)
        foreach (var rtl in new[] { RegexOptions.None, RegexOptions.RightToLeft })
        {
            var options = RegexOptions.CultureInvariant | rtl;
            var tree = Utf8OwnedTreeParser.TryParse(pattern, options);
            Assert.NotNull(tree);
            var sets = Extract(tree.Root);
            var facts = Utf8FrontEndSearchAnalyzer.Analyze(Utf8FrontEnd.Analyze(pattern, options).SemanticRegex);
            var selected = facts.FixedDistanceSets;
            var program = Utf8OwnedTreeSliceLowerer.TryLower(tree, options);
            Assert.NotNull(program);
            var starts = new int[program.GroupCount];
            var ends = new int[program.GroupCount];
            foreach (var input in subjects)
            for (var boundary = 0; boundary <= input.Length; boundary++)
            {
                if (boundary < input.Length && (input[boundary] & 0xc0) == 0x80) continue;
                var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(100_000, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
                // Run the scalar VM directly; neither discovery nor confirmation
                // consumes the candidate facts that this witness is qualifying.
                var match = Utf8SliceRunner.TryMatchAt(program, input, boundary, starts, ends, true, ref invocation);
                Assert.Equal(Utf8ExecutionLimitKind.None, match.Breach);
                if (!match.IsMatch) continue;
                observed++;
                var start = Math.Min(boundary, match.EndIndex);
                var length = Math.Abs(boundary - match.EndIndex);
                Check(sets, start);
                var selectedBase = start;
                if (facts.FallbackStartTransform.Kind == Utf8FallbackStartTransformKind.TrimLeadingAsciiWhitespace)
                    while (selectedBase < start + length && Utf8AsciiBytePredicates.IsSixByteWhitespace(input[selectedBase])) selectedBase++;
                Check(selected, selectedBase);

                void Check(Utf8FixedDistanceSet[] constraints, int origin)
                {
                    foreach (var set in constraints)
                    {
                        Assert.InRange(set.Distance, 0, start + length - origin - 1);
                        Assert.True(set.Contains(input[origin + set.Distance]), $"{pattern} / {rtl} / {Encoding.UTF8.GetString(input)}: {set.Distance}");
                    }
                }
            }
        }
        Assert.True(observed > 40);
        foreach (var row in new (string Pattern, string Subject)[]
        {
            ("(ab.)cd", "ab😀cd"), ("(ab.*)cd", "ab😀écd"), ("(ab(?i:k))cd", "abKcd"),
            ("a[é-ê]{2}bc", "aéêbc"), ("(?i:ab)cd", "ABcd"), ("\\s*abc[0-9]", "  abc7"), ("\\s*abc[0-9]", "\u00a0abc7"),
        })
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.Compiled | RegexOptions.RightToLeft })
        {
            var input = Encoding.UTF8.GetBytes(row.Subject);
            var regex = new Utf8Regex(row.Pattern, RegexOptions.CultureInvariant | mode);
            Assert.True(regex.IsMatch(input));
            Assert.Equal(1, regex.Count(input));
            var match = regex.Match(input);
            var detailed = regex.MatchDetailed(input);
            Assert.Equal((0, input.Length), (match.IndexInBytes, match.LengthInBytes));
            Assert.Equal((0, input.Length), (detailed.IndexInBytes, detailed.LengthInBytes));
        }
    }

    [Fact]
    public void PrefixAdmissionRestoresLoansAndChargesRealWrapperDepth()
    {
        var nodes = new[] { "(ab.)cd", "(ab😀)cd", "(?i:abc)de", "[A-Z][0-9]xy", "(?:a|b).*", "" }
            .Select(pattern => Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant)).ToArray();
        foreach (var tree in nodes)
        {
            Assert.NotNull(tree);
            ulong work;
            int[] expected;
            using (var measured = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
            {
                var sets = Extract(tree.Root);
                expected = sets.Select(set => set.Distance).ToArray();
                work = measured.Budget.WorkSteps;
                if (sets.Length != 0) Assert.True(measured.Budget.ReservedBytes >= Utf8ConstructionBudget.ArrayBytes<Utf8FixedDistanceSet>(sets.Length));
            }
            Assert.InRange(work, 1ul, 10_000ul);
            for (ulong cutoff = 0; cutoff <= work; cutoff++)
            {
                using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 1, ulong.MaxValue);
                var parent = owner.Budget.AllocateArray<byte>(1, false);
                var checkpoint = owner.Budget.ObjectCheckpoint;
                Exception? failure;
                using (var child = Utf8ConstructionScope.Enter())
                    failure = Record.Exception(() => Assert.Equal(expected, Extract(tree.Root).Select(set => set.Distance)));
                if (cutoff == work) Assert.Null(failure);
                else Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                Assert.Equal(48ul, owner.Budget.ReservedBytes);
                Assert.Equal(0ul, owner.Budget.Depth);
                Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
                Assert.Single(parent);
                Assert.InRange(owner.Budget.WorkSteps, 1ul, cutoff + 1);
            }
        }
        var deep = Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.CultureInvariant, "abc");
        for (var index = 0; index < 32; index++)
            deep = Utf8TreeNode.Create(Utf8TreeNodeKind.Group, RegexOptions.CultureInvariant).WithChild(deep);
        using (var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 4))
        {
            var parent = owner.Budget.AllocateArray<byte>(1, false);
            var checkpoint = owner.Budget.ObjectCheckpoint;
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Extract(deep));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
            Assert.Equal(5ul, failure.Requested);
            Assert.Equal(48ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            Assert.Single(parent);
        }
        foreach (var denial in new[] { 0, 1, 2 })
        {
            using var owner = new Utf8ConstructionScope(0, denial == 0 ? 48ul : ulong.MaxValue,
                denial == 2 ? 1ul : ulong.MaxValue, denial == 1 ? 0ul : ulong.MaxValue);
            var parent = owner.Budget.AllocateArray<byte>(1, false);
            var checkpoint = owner.Budget.ObjectCheckpoint;
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Extract(deep));
            Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes, 1 => Utf8ResourceLimitKind.ConstructionDepth,
                _ => Utf8ResourceLimitKind.ConstructionWorkSteps }, failure.ResourceKind);
            Assert.Equal(48ul, owner.Budget.PeakBytes);
            Assert.Equal(48ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            Assert.Single(parent);
        }
    }
}
