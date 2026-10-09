using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FindByteFactsTests
{
    public static IEnumerable<object?[]> WidthCases()
    {
        var cases = new (string Pattern, int Minimum, int? Maximum)[]
        {
            ("😀", 4, 4), ("a😀é", 7, 7), (".", 1, 4), ("[😀-🙏]", 4, 4),
            ("[a😀]", 1, 4), ("[éê]", 2, 2), ("[中]", 3, 3), ("[^a]", 1, 4),
            (@"\d", 1, 4), (@"\p{L}", 1, 4), ("(?i:k)", 1, 3), ("(?i:a)", 1, 1),
            ("(?i:é)", 2, 2), (@"(a)\1", 1, null), (@"(?<x>😀)\k<x>", 4, null),
            ("(?(?=.{2})é|abc)", 2, 3), ("(a)?(?(1)é)", 0, 3),
            ("(?(?=a)😀|😀)", 4, 4), ("(?<=😀)é", 2, 2), ("(?=😀)", 0, 0),
            ("(?:é){2,3}", 4, 6), ("(?:😀){2}", 8, 8), ("(?:😀?){3}", 0, 12),
            ("é{2147483647}", int.MaxValue, null), ("^*", 0, 0), (@"\b+", 0, 0),
        };
        foreach (var row in cases)
        foreach (var options in new[] { RegexOptions.CultureInvariant,
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            RegexOptions.CultureInvariant | RegexOptions.RightToLeft,
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.RightToLeft })
            yield return [row.Pattern, options, row.Minimum, row.Maximum];
    }

    [Theory]
    [MemberData(nameof(WidthCases))]
    public void BoundsDescribeConsumedScalarBytes(string pattern, RegexOptions options, int minimum, int? maximum)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, options) ?? throw new InvalidOperationException();
        var width = Utf8OwnedFindOptimizations.ComputeByteWidth(tree.Root);
        Assert.Equal(minimum, width.Minimum);
        Assert.Equal(maximum, width.Maximum);
        Assert.Equal(maximum is int high && high == minimum, width.IsFixed);
    }

    [Theory]
    [InlineData("éab", "éab", 0)]
    [InlineData("(é)[ab]cd", "cd", 3)]
    [InlineData("(😀)[ab]cd", "cd", 5)]
    [InlineData("[😀-🙏]ab", "ab", 4)]
    [InlineData("[éê]ab", "ab", 2)]
    [InlineData("(?:é|ê)ab", "ab", 2)]
    [InlineData("(?:é|中)ab", null, 0)]
    [InlineData(".ab", null, 0)]
    [InlineData("a{2,4}bc", null, 0)]
    [InlineData("(?:é){2}ab", "ab", 4)]
    [InlineData("ab.*longer", "ab", 0)]
    [InlineData(@"(a)\1longer", "a", 0)]
    [InlineData("(?i:k)ab", null, 0)]
    [InlineData("(?i:é)ab", "ab", 2)]
    [InlineData("(?(?=x)é|ê)ab", "ab", 2)]
    [InlineData("(?(?=x)é|中)ab", null, 0)]
    public void FixedDistancesRequireAConstantBytePrefix(string pattern, string? literal, int distance)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant) ?? throw new InvalidOperationException();
        var result = Utf8OwnedFindOptimizations.TryExtractFixedDistanceLiteral(tree.Root);
        if (literal is null) Assert.Null(result);
        else Assert.Equal(((string Literal, int Distance)?)(literal, distance), result);
    }

    [Theory]
    [InlineData("(é)[ab]", 2)]
    [InlineData("(😀)[ab]", 4)]
    public void AsciiSetDistancesIncludeTheWholeEncodedPrefix(string pattern, int distance)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant) ?? throw new InvalidOperationException();
        var sets = Utf8OwnedFindOptimizations.FindFixedDistanceSets(tree.Root);
        Assert.NotNull(sets);
        Assert.Equal(distance, Assert.Single(sets).Distance);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(true, true)]
    public void PublicSearchKeepsVariableScalarPrefixesAndByteCoordinates(bool compiled, bool rtl)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        foreach (var row in new[] { (Pattern: "(?:é|中)ab", Subject: "中ab"), (Pattern: ".ab", Subject: "😀ab"),
            (Pattern: "a{2,4}bc", Subject: "aaaabc"),
            (Pattern: rtl ? @"\k<x>(?<x>😀)ab" : @"(?<x>😀)\k<x>ab", Subject: "😀😀ab") })
        {
            var regex = new Utf8Regex(row.Pattern, options);
            var input = Encoding.UTF8.GetBytes("!" + row.Subject + "!");
            Assert.True(regex.IsMatch(input), row.Pattern);
            Assert.Equal(1, regex.Count(input));
            var match = regex.Match(input);
            Assert.True(match.Success);
            Assert.True(match.IsByteAligned);
            Assert.Equal(1, match.IndexInBytes);
            Assert.Equal(Encoding.UTF8.GetByteCount(row.Subject), match.LengthInBytes);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void BorrowedScalarValidationAndFixedProofNeverRefundSpentWork(int route)
    {
        var text = string.Concat(Enumerable.Repeat("😀", 100000));
        var node = Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.None, text);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, 1000, ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            if (route == 0) Utf8OwnedFindOptimizations.ComputeByteWidth(node);
            else Utf8OwnedFindOptimizations.TryExtractFixedDistanceLiteral(node);
        });
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(1000ul, owner.Budget.WorkSteps);
        Assert.Single(parent);
        var spent = owner.Budget.WorkSteps;
        using (var recovery = Utf8ConstructionScope.Enter())
            Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps,
                Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                    Utf8OwnedFindOptimizations.ComputeByteWidth(Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.None, 'a'))).ResourceKind);
        Assert.Equal(spent, owner.Budget.WorkSteps);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
    }

    [Fact]
    public void NonInvariantCaseBoundsCoverCultureDependentByteWidths()
    {
        var node = Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.IgnoreCase, 'I');
        Assert.Equal(new Utf8OwnedFindOptimizations.ByteWidth(1, 2), Utf8OwnedFindOptimizations.ComputeByteWidth(node));
    }

    [Fact]
    public void ObsoleteWidthWrappersAndLiteralHolderAreAbsentFromMetadata()
    {
        Assert.Null(typeof(Utf8OwnedFindOptimizations).GetMethod("ComputeMinLength"));
        Assert.Null(typeof(Utf8OwnedFindOptimizations).GetMethod("ComputeMaxLength"));
        Assert.Null(typeof(Utf8OwnedFindOptimizations).GetNestedType("LiteralHolder", System.Reflection.BindingFlags.NonPublic));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void FixedLiteralProofAdmitsItsRootBeforeAnyAcquisition(int denial)
    {
        var node = Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.None, 'a');
        using var owner = new Utf8ConstructionScope(0, denial == 0 ? 48ul : ulong.MaxValue,
            denial == 2 ? 1ul : ulong.MaxValue, denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8OwnedFindOptimizations.TryExtractFixedDistanceLiteral(node));
        Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes,
            1 => Utf8ResourceLimitKind.ConstructionDepth, _ => Utf8ResourceLimitKind.ConstructionWorkSteps }, failure.ResourceKind);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(48ul, owner.Budget.PeakBytes);
        Assert.Equal(1ul, owner.Budget.WorkSteps);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Single(parent);
    }
}
