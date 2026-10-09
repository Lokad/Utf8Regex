using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8WindowByteBoundsTests
{
    public static IEnumerable<object[]> Bounds()
    {
        var rows = new (string Pattern, int? Maximum, bool Excludes)[]
        {
            ("(?i:k)", 3, true), ("(?i:K)", 3, true), ("(?i:[k])", 3, true),
            ("(?i:[a-z])", 3, true), ("(?i:é)", 2, true), ("😀", 4, true),
            (".", 4, false), (@"[\s\S]", 4, false), (@"\d", 4, true),
            (@"\s", 4, false), ("[😀-🙏]", 4, true), ("[^a]", 4, false),
            (@"[^\r\n]", 4, true), ("[a-z-[aeiou]]", 4, false),
            ("(?(?=\n)ab|é)", 2, false), ("(?=\n)é", 2, false),
            ("(?=\n)", 0, true), ("\n{0}", 0, true), ("(?:\n){0}", 0, true),
            ("(?:😀){2,3}", 12, true), ("(?:é|😀){2}", 8, true),
            ("ab[中]{1,3}cd", 13, true), ("a{2147483646}b", int.MaxValue, true),
            ("a{2147483646}bc", null, false),
        };
        foreach (var row in rows)
        foreach (var options in new[] { RegexOptions.CultureInvariant,
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            RegexOptions.CultureInvariant | RegexOptions.RightToLeft,
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.RightToLeft })
            yield return [row.Pattern, options, row.Maximum, row.Excludes];
    }

    [Theory]
    [MemberData(nameof(Bounds))]
    public void WindowWidthsAreScalarByteUpperBounds(string pattern, RegexOptions options, int? maximum, bool excludes)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, options);
        Assert.NotNull(tree);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var result = Width(tree.Root);
        Assert.Equal(maximum.HasValue, result.Success);
        Assert.Equal(maximum.GetValueOrDefault(), result.Width);
        Assert.Equal(excludes, result.Excludes);
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(0ul, owner.Budget.Reserved);
    }

    [Theory]
    [InlineData("(?i:k){1,2}", 6, true)]
    [InlineData("[😀-🙏]{1,2}", 8, true)]
    [InlineData(".{1,2}", 8, false)]
    [InlineData(@"[^\r\n]{1,2}", 8, true)]
    [InlineData("[a-z-[aeiou]]{1,2}", 8, false)]
    [InlineData("(?=\n)", 0, true)]
    [InlineData("[a-z]{2147483646}[0-9]{2}", null, false)]
    public void OrderedWindowFactsUseBytesAndDeclineCombinedOverflow(string gap, int? maximum, bool excludes)
    {
        var semantic = Utf8FrontEnd.Compile("(?:first|second)" + gap + "last", RegexOptions.CultureInvariant).SemanticRegex;
        var facts = Utf8FrontEndSearchAnalyzer.Analyze(semantic);
        Assert.Equal(maximum, facts.OrderedWindowMaxGap);
        Assert.Equal(excludes, facts.OrderedWindowSameLine);
    }

    [Theory]
    [InlineData(RegexOptions.CultureInvariant)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.RightToLeft)]
    public void FoldedAndSupplementaryWindowsKeepOwnedByteOperations(RegexOptions options)
    {
        foreach (var row in new[]
        {
            (Pattern: "(first|second)(?i:k){1,2}last", Text: "!firstKlast!secondkKlast!", First: 12, Second: 14),
            (Pattern: "(first|second)[😀-🙏]{1,2}last", Text: "!first😀last!second😀🙏last!", First: 13, Second: 18),
            (Pattern: "(first|second).{1,2}last", Text: "!first😀last!secondé😀last!", First: 13, Second: 16),
        })
        {
            var regex = new Utf8Regex(row.Pattern, options);
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
            Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
            var property = typeof(Utf8Regex).GetProperty("_verifierRuntime", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(property);
            var runtime = Assert.IsType<Utf8VerifierRuntime>(property.GetValue(regex));
            foreach (var name in new[] { "_fallbackRegex", "_anchoredFallbackRegex" })
            {
                var field = typeof(Utf8FallbackCandidateVerifier).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(field);
                field.SetValue(runtime.FallbackCandidateVerifier, new Lazy<Regex>(() =>
                    throw new InvalidOperationException("Window matching accessed BCL fallback.")));
            }
            var input = Encoding.UTF8.GetBytes(row.Text);
            Assert.True(regex.IsMatch(input));
            Assert.Equal(2, regex.Count(input));
            var rtl = (options & RegexOptions.RightToLeft) != 0;
            var match = regex.Match(input);
            Assert.True(match.Success);
            Assert.Equal(rtl ? row.First + 2 : 1, match.IndexInBytes);
            Assert.Equal(rtl ? row.Second : row.First, match.LengthInBytes);
            var context = regex.MatchDetailed(input);
            Assert.Equal(match.IndexInBytes, context.IndexInBytes);
            Assert.Equal(match.LengthInBytes, context.LengthInBytes);
            Assert.Equal(rtl ? "second" : "first", context.GetGroup(1).GetValueString());
            var enumerator = regex.EnumerateMatches(input);
            var count = 0;
            while (enumerator.MoveNext())
            {
                var second = rtl ? count == 0 : count == 1;
                Assert.Equal(second ? row.First + 2 : 1, enumerator.Current.IndexInBytes);
                Assert.Equal(second ? row.Second : row.First, enumerator.Current.LengthInBytes);
                count++;
            }
            Assert.Equal(2, count);
            Assert.Equal("!X!X!", regex.ReplaceToString(input, "X"));
            var splits = new List<string>();
            foreach (var split in regex.EnumerateSplits(input)) splits.Add(split.GetValueString());
            Assert.Equal(new[] { "!", "!", "!" }, splits);
            Assert.False(regex.IsMatch("firstlast"u8));
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
            Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        }
    }

    [Fact]
    public void LongPlainClassLineProofIsAdmittedBeforeMembershipScanning()
    {
        var wire = "\0" + (char)32000 + "\0" + string.Concat(Enumerable.Repeat("ab", 16000));
        var node = Utf8TreeNode.Create(Utf8TreeNodeKind.Set, RegexOptions.CultureInvariant, wire);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, 40000, ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Width(node));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, error.ResourceKind);
        Assert.True(error.Requested > 40000);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(0, parent[0]);
        Assert.Throws<Utf8ResourceLimitExceededException>(() => Width(node));
        owner.Budget.ReleaseArray(parent, false);
    }

    [Fact]
    public void RetiredWindowWidthFormulasAndUnusedFrameStateAreAbsent()
    {
        var type = typeof(Utf8FrontEndSearchAnalyzer);
        foreach (var name in new[] { "TryGetFiniteAsciiWindowWidth", "TryGetFiniteRepeatedWidth",
            "TryGetAsciiSetWidthAndLineSafety", "ContainsNewlineRange" })
            Assert.Null(type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static));
        Assert.Null(type.GetNestedType("WidthFrame", BindingFlags.NonPublic));
        var frame = type.GetNestedType("LineBreakFrame", BindingFlags.NonPublic);
        Assert.NotNull(frame);
        Assert.Null(frame.GetField("Excludes", BindingFlags.NonPublic | BindingFlags.Instance));
    }

    private static (bool Success, int Width, bool Excludes) Width(Utf8TreeNode node)
    {
        var method = typeof(Utf8FrontEndSearchAnalyzer).GetMethod("TryGetFiniteScalarWindowByteWidth", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        object?[] args = [node, 0, false];
        try
        {
            var success = Assert.IsType<bool>(method.Invoke(null, args));
            return (success, Assert.IsType<int>(args[1]), Assert.IsType<bool>(args[2]));
        }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Capture(inner).Throw();
            throw;
        }
    }
}
