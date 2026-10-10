using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8WindowByteBoundsTests
{
    [Fact]
    public void LeadingWindowFamiliesKeepPunctuationBranchesAndAdmitEveryWorkCutoff()
    {
        var semantic = Utf8FrontEnd.Compile("(?:foo|!!).{0,2}bar", RegexOptions.CultureInvariant).SemanticRegex;
        var facts = Utf8FrontEndSearchAnalyzer.Analyze(semantic);
        Assert.Equal(["foo", "!!"], facts.OrderedWindowLeadingLiteralsUtf8.Select(Encoding.UTF8.GetString));
        var partial = Utf8FrontEndSearchAnalyzer.Analyze(Utf8FrontEnd.Compile("(foo\\s+bar).{0,2}baz", RegexOptions.CultureInvariant).SemanticRegex);
        Assert.Equal(["foo"], partial.OrderedWindowLeadingLiteralsUtf8.Select(Encoding.UTF8.GetString));
        Assert.Null(partial.OrderedWindowMaxGap);
        var optional = Utf8FrontEndSearchAnalyzer.Analyze(Utf8FrontEnd.Compile("((foo)(bar))(baz)?", RegexOptions.CultureInvariant).SemanticRegex);
        Assert.Empty(optional.OrderedWindowLeadingLiteralsUtf8);
        var method = typeof(Utf8FrontEndSearchAnalyzer).GetMethod("TryExtractLeadingWindowPhrases", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        foreach (var pattern in new[] { "foo", "foo|!!|foo", "(foo)(bar)" })
        {
            var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
            Assert.NotNull(tree);
            ulong work;
            string[] expected;
            using (var measured = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
            {
                expected = Invoke(tree.Root)!;
                Assert.NotNull(expected);
                work = measured.Budget.WorkSteps;
                Assert.True(measured.Budget.ReservedBytes >= Utf8ConstructionBudget.ArrayBytes<string>(expected.Length));
            }
            Assert.InRange(work, 1ul, 10_000ul);
            for (ulong cutoff = 0; cutoff <= work; cutoff++)
            {
                using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 1, ulong.MaxValue);
                var parent = owner.Budget.AllocateArray<byte>(1, false);
                var checkpoint = owner.Budget.ObjectCheckpoint;
                Exception? failure;
                using (var child = Utf8ConstructionScope.Enter()) failure = Record.Exception(() => Assert.Equal(expected, Invoke(tree.Root)));
                if (cutoff == work) Assert.Null(failure);
                else Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                Assert.Equal(48ul, owner.Budget.ReservedBytes);
                Assert.Equal(0ul, owner.Budget.Depth);
                Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
                Assert.Single(parent);
                Assert.InRange(owner.Budget.WorkSteps, 1ul, cutoff + 1);
            }
            foreach (var depth in new[] { false, true })
            {
                using var owner = new Utf8ConstructionScope(0, depth ? ulong.MaxValue : 48, ulong.MaxValue, depth ? 0ul : ulong.MaxValue);
                var parent = owner.Budget.AllocateArray<byte>(1, false);
                var checkpoint = owner.Budget.ObjectCheckpoint;
                var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Invoke(tree.Root));
                Assert.Equal(depth ? Utf8ResourceLimitKind.ConstructionDepth : Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
                Assert.Equal(48ul, owner.Budget.ReservedBytes);
                Assert.Equal(48ul, owner.Budget.PeakBytes);
                Assert.Equal(0ul, owner.Budget.Depth);
                Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
                Assert.Single(parent);
            }
        }
        var folded = Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.IgnoreCase, "foo");
        Assert.Null(Invoke(folded));

        string[]? Invoke(Utf8TreeNode node)
        {
            object?[] arguments = [node, null];
            try { return Assert.IsType<bool>(method.Invoke(null, arguments)) ? Assert.IsType<string[]>(arguments[1]) : null; }
            catch (TargetInvocationException error) when (error.InnerException is { } inner)
            { ExceptionDispatchInfo.Capture(inner).Throw(); throw; }
        }
    }

    [Fact]
    public void OrderedWindowFactsCoverIndependentScalarVmMatchesAndGapBounds()
    {
        var patterns = new[] { "(?:foo|!!).{0,2}bar", "(foo\\s+bar).{0,2}baz", "(?:foo\\s+bar|!!).{0,2}baz",
            "((foo)(bar))(baz)?", "foo.{0,2}(?i:bar)", "(?i:foo).{0,2}bar", "(?:first|second)[😀-🙏]{1,2}last",
            "(?:first|second)(?i:k){1,2}last", "(?:first|second)[^\\r\\n]{0,2}last", "foo(bar)(baz)",
            "(?:foo|bar).{0,2}baz", "foo\\s*bar", "\\s*# [Nn][Oo][Qq][Aa]", "(?:foo|!!)xbar" };
        var subjects = new[] { "fooxbar", "!!xbar", "!!xbar!fooxbar", "foo😀bar", "foo😀BAR", "FOO😀bar",
            "foobar", "foobarbaz", "foo\tbarbaz", "foo            barbaz", "foo\u00a0barbaz", "!!xbaz",
            "first😀last", "second😀🙏last", "firstKlast", "secondkKlast", "firsté😀last", "first\nlast", "# NOQA" }
            .Select(Encoding.UTF8.GetBytes).ToArray();
        var observed = 0;
        foreach (var pattern in patterns)
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.RightToLeft })
        {
            var semantic = Utf8FrontEnd.Compile(pattern, options).SemanticRegex;
            var facts = Utf8FrontEndSearchAnalyzer.Analyze(semantic);
            if (facts.OrderedWindowLeadingLiteralsUtf8.Length == 0 || facts.OrderedWindowTrailingLiteralUtf8.Length == 0) continue;
            var program = Utf8OwnedTreeSliceLowerer.TryLower(semantic.RuntimeTree!, options);
            Assert.NotNull(program);
            var starts = new int[program.GroupCount];
            var ends = new int[program.GroupCount];
            foreach (var input in subjects)
            for (var boundary = 0; boundary <= input.Length; boundary++)
            {
                if (boundary < input.Length && (input[boundary] & 0xc0) == 0x80) continue;
                var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(100_000, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
                var match = Utf8SliceRunner.TryMatchAt(program, input, boundary, starts, ends, true, ref invocation);
                Assert.Equal(Utf8ExecutionLimitKind.None, match.Breach);
                if (!match.IsMatch) continue;
                observed++;
                var value = input.AsSpan(Math.Min(boundary, match.EndIndex), Math.Abs(boundary - match.EndIndex));
                var covered = false;
                foreach (var leading in facts.OrderedWindowLeadingLiteralsUtf8)
                for (var offset = 0; offset + leading.Length <= value.Length; offset++)
                {
                    if (!value[offset..].StartsWith(leading)) continue;
                    for (var end = offset + leading.Length; end + facts.OrderedWindowTrailingLiteralUtf8.Length <= value.Length; end++)
                    {
                        if (!value[end..].StartsWith(facts.OrderedWindowTrailingLiteralUtf8)) continue;
                        var gap = value.Slice(offset + leading.Length, end - offset - leading.Length);
                        if (facts.OrderedWindowMaxGap is int maximum && gap.Length > maximum) continue;
                        if (facts.OrderedWindowSameLine && (gap.Contains((byte)'\n') || gap.Contains((byte)'\r'))) continue;
                        covered = true;
                    }
                }
                Assert.True(covered, $"Uncovered ordered window: {pattern} / {options}");
            }
        }
        Assert.True(observed > 10);
    }

    public static IEnumerable<object[]> Bounds()
    {
        var rows = new (string Pattern, int? Maximum, bool Excludes)[]
        {
            ("(?i:k)", 3, true), ("(?i:K)", 3, true), ("(?i:[k])", 3, true),
            ("(?i:[a-z])", 3, true), ("(?i:é)", 2, true), ("😀", 4, true),
            (".", 4, false), (@"[\s\S]", 4, false), (@"\d", 4, true),
            (@"\s", 4, false), ("[😀-🙏]", 4, true), ("[^a]", 4, false),
            (@"[^\r\n]", 4, true), ("[a-z-[aeiou]]", 4, true),
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
    [InlineData("[a-z-[aeiou]]{1,2}", 8, true)]
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
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
            Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
            var property = typeof(Utf8Regex).GetProperty("_verifierRuntime", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(property);
            var runtime = Assert.IsType<Utf8VerifierRuntime>(property.GetValue(regex));
            Utf8OwnedVerifierAssertions.RetainsPreparedOwner(runtime);
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
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
            Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        }
    }

    [Fact]
    public void LongPlainClassLineProofIsAdmittedBeforeMembershipScanning()
    {
        Utf8TreeNode node;
        using (var construction = Utf8ConstructionScope.Enter())
        {
            using var data = Utf8ConstructionBuffer<int>.Create(construction.Budget);
            data.AddRange([0, 32000, 0]);
            for (var index = 0; index < 16000; index++) data.AddRange(['a', 'b']);
            node = Utf8TreeNode.Create(Utf8TreeNodeKind.Set, RegexOptions.CultureInvariant, Utf8ScalarClass.Create(data));
            construction.Complete();
        }
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
