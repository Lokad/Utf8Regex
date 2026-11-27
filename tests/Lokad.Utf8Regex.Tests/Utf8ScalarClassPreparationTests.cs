using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarClassPreparationTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static Utf8ScalarClass Class(string pattern)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(tree);
        var node = tree.Root;
        while (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group) node = node.Child(0);
        Assert.NotNull(node.ScalarClass);
        return node.ScalarClass;
    }
    [Theory]
    [InlineData(@"[a-z]")] [InlineData(@"[^a]")] [InlineData(@"[\s-[\t]]")] [InlineData(@"[\s-[\s]]")]
    [InlineData(@"[^\S-[\t]]")] [InlineData(@"[\s-[\t-[\r]]]")] [InlineData(@"[^a-[^b]]")] [InlineData(@"[\p{L}-[A-Z]]")]
    [InlineData(@"\s")] [InlineData(@"\S")] [InlineData(@"[\S\t]")] [InlineData(@"[\s\S]")]
    public void PreparedClassQueriesAndScalarMatchingAgreeOnAsciiOverlap(string pattern)
    {
        var scalarClass = Class(pattern);
        var comparator = new Regex(@"\A(?:" + pattern + @")\z", RegexOptions.CultureInvariant);
        using var regex = new Utf8Regex(@"\A(" + pattern + @")\z", RegexOptions.CultureInvariant);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue);
        Assert.True(Utf8ScalarAsciiProjector.TryProjectAsciiQuery(scalarClass, out var mask));
        Span<byte> input = stackalloc byte[1];
        for (var value = 0; value < 128; value++)
        {
            input[0] = (byte)value;
            var expected = comparator.IsMatch(((char)value).ToString());
            Assert.Equal(expected, mask.Contains(input[0]));
            Assert.Equal(expected, regex.IsMatch(input));
        }
        foreach (var text in new[] { "\u0085", "\u2003", "é" })
            Assert.Equal(comparator.IsMatch(text), regex.IsMatch(Encoding.UTF8.GetBytes(text)));
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.PeakBytes);
    }

    [Theory]
    [InlineData(@"[\u0000-\u007f]", true, 128)]
    [InlineData(@"[\u003f-\u0040]", true, 2)]
    [InlineData(@"[\u0040-\u007f]", true, 64)]
    [InlineData(@"[\u007f-\u0100]", false, 1)]
    [InlineData(@"[\u0080-\u0100]", false, 0)]
    public void RangeProjectionPreservesWordEdgesAndClipsOnlyQueries(string pattern, bool complete, int asciiCount)
    {
        var scalarClass = Class(pattern);
        Assert.True(Utf8ScalarAsciiProjector.TryProjectAsciiQuery(scalarClass, out var query));
        Assert.Equal(asciiCount, query.Count);
        Assert.Equal(complete, Utf8ScalarAsciiProjector.TryProjectPositiveAsciiDefinition(scalarClass, out var definition));
        Assert.Equal(complete, Utf8ScalarAsciiProjector.TryCreateToken(scalarClass, out var token));
        if (complete)
        {
            Assert.Equal(query, definition);
            Assert.Equal(query, token.CharClass);
        }
        var oracle = new Regex(@"\A" + pattern + @"\z", RegexOptions.CultureInvariant);
        using var regex = new Utf8Regex(@"\A" + pattern + @"\z");
        Span<byte> input = stackalloc byte[1];
        for (var value = 0; value < 128; value++)
        {
            input[0] = (byte)value;
            var expected = oracle.IsMatch(((char)value).ToString());
            Assert.Equal(expected, query.Contains(input[0]));
            Assert.Equal(expected, regex.IsMatch(input));
        }
        Assert.Equal(oracle.IsMatch("\u0080"), regex.IsMatch("\u0080"u8));
    }

    [Fact]
    public void FoldedClassPayloadIsAdmittedOnceAndRollsBackOnObjectDenial()
    {
        int[] definition = [0, 4, 0, 'A', 'B', 'a', 'b'];
        var arrayBytes = Utf8ConstructionBudget.ArrayBytes<int>(definition.Length);
        var total = arrayBytes + Math.Max(128ul, Utf8ScalarClass.ObjectBytes);
        using (var denied = new Utf8ConstructionScope(0, total - 1))
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8ScalarClass.Create(definition));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
            Assert.Equal(total, failure.Requested);
            Assert.Equal(arrayBytes, denied.Budget.PeakBytes);
            Assert.Equal(0ul, denied.Budget.ReservedBytes);
        }
        using var owner = new Utf8ConstructionScope(0, total);
        var retained = Utf8ScalarClass.Create(definition);
        Array.Clear(definition);
        Assert.Equal([0, 4, 0, 'A', 'B', 'a', 'b'], retained.Data);
        Assert.Equal(total, owner.Budget.ReservedBytes);
        Assert.Equal(total, owner.Budget.PeakBytes);
    }

    [Fact]
    public void CanonicalAsciiQueriesCoverPredicatesAndClassesBeyondTheFormerProjectionLimit()
    {
        foreach (var pattern in new[] { @"\p{Lu}", @"\P{Ll}", @"[\p{L}\p{N}]", @"[^\p{L}\p{N}]",
            @"\p{IsBasicLatin}", @"\P{IsBasicLatin}", @"[\P{IsGreek}0-9]", @"\p{Cs}",
            @"[\p{L}-[A-Z-[M]]]", @"[\P{L}-[^a-[b]]]", @"[\S\t]", @"[\s-[\S]]" })
        {
            var scalarClass = Class(pattern);
            var comparator = new Regex(@"\A(?:" + pattern + @")\z", RegexOptions.CultureInvariant);
            using var query = new Utf8ConstructionScope(0, 0, ulong.MaxValue);
            Assert.True(Utf8ScalarAsciiProjector.TryProjectAsciiQuery(scalarClass, out var mask));
            for (var value = 0; value < 128; value++)
                Assert.Equal(comparator.IsMatch(((char)value).ToString()), mask.Contains((byte)value));
            Assert.Equal(0ul, query.Budget.PeakBytes);
        }

        Utf8ScalarClass large;
        using (var preparation = Utf8ConstructionScope.Enter())
        {
            using var data = Utf8ConstructionBuffer<int>.Create(preparation.Budget);
            data.AddRange([0, 65540, 0]);
            for (var index = 0; index < 32770; index++)
            {
                data.Add(0x10000 + 2 * index);
                data.Add(0x10001 + 2 * index);
            }
            large = Utf8ScalarClass.Create(data);
            preparation.Complete();
        }
        Assert.True(Utf8ScalarAsciiProjector.TryProjectAsciiQuery(large, out var warmed));
        Assert.True(warmed.IsEmpty);
        using (var query = new Utf8ConstructionScope(0, 128, ulong.MaxValue))
        {
            var parent = query.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = query.Budget.ReservedBytes;
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            var succeeded = Utf8ScalarAsciiProjector.TryProjectAsciiQuery(large, out var mask);
            var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Assert.True(succeeded);
            Assert.True(mask.IsEmpty);
            Assert.Equal(0, bytes);
            Assert.Equal(128ul, query.Budget.ReservedBytes);
            Assert.Equal(128ul, query.Budget.PeakBytes);
            Assert.Equal(0ul, query.Budget.Depth);
            Assert.Equal(checkpoint, query.Budget.ReservedBytes);
            Assert.Equal(17, parent.Value);
        }
        using (var query = new Utf8ConstructionScope(0, 128, ulong.MaxValue))
        {
            var parent = query.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = query.Budget.ReservedBytes;
            Assert.Equal(128ul, query.Budget.ReservedBytes);
            Assert.Equal(0ul, query.Budget.Depth);
            Assert.Equal(checkpoint, query.Budget.ReservedBytes);
            Assert.Equal(17, parent.Value);
        }
    }
    [Theory]
    [InlineData(RegexOptions.CultureInvariant)] [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.RightToLeft)] [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.RightToLeft)]
    public void StructuralSuffixesKeepOwnedScalarByteOperations(RegexOptions options)
    {
        foreach (var row in new[]
        {
            (Pattern: @"(?:call|invoke)[ \t]+([A-Z][a-z]*)[ \t]*=[ \t]*await", First: "call Name = await", Second: "invoke Other = await"),
            (Pattern: @"(?:using|await)[ \t]+([A-Z][a-z]*)[ \t]*\([ \t]*\)", First: "using Name()", Second: "await Other ( )"),
        })
        {
            var regex = new Utf8Regex(row.Pattern, options: options);
            var input = Encoding.UTF8.GetBytes("😀" + row.First + "中" + row.Second + "😀");
            Assert.True(regex.IsMatch(input)); Assert.Equal(2, regex.Count(input));
            var rtl = (options & RegexOptions.RightToLeft) != 0; var match = regex.Match(input);
            Assert.True(match.Success); Assert.Equal(rtl ? 7 + row.First.Length : 4, match.StartOffsetInBytes);
            Assert.Equal(rtl ? row.Second.Length : row.First.Length, (match.EndOffsetInBytes - match.StartOffsetInBytes));
            var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input)); Assert.Equal(match.StartOffsetInBytes, detailed.Value.StartOffsetInBytes);
            Assert.Equal(rtl ? "Other" : "Name", System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(1).Span));
            var count = 0; foreach (var item in regex.EnumerateMatches(input)) { Assert.True((item.EndOffsetInBytes - item.StartOffsetInBytes) > 0); count++; }
            Assert.Equal(2, count); Assert.Equal("😀X中X😀", Encoding.UTF8.GetString(regex.Replace(input, "X"u8)));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null); Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        }
    }
}
