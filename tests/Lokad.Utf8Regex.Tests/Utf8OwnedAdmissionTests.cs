using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8OwnedAdmissionTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }

    [Theory]
    [InlineData("[-[a]]", "x-]a][]y", false)]
    [InlineData("[-[a]]", "x-]a][]y", true)]
    [InlineData(@"\k<>", "😀k<>k<>", false)]
    [InlineData(@"\k<>", "😀k<>k<>", true)]
    [InlineData(@"\k<x", "😀k<xk<x", false)]
    [InlineData(@"\k<x", "😀k<xk<x", true)]
    [InlineData(@"\k'x>", "😀k'x>k'x>", false)]
    [InlineData(@"\k'x>", "😀k'x>k'x>", true)]
    [InlineData(@"(?<n>a)\k< n>", "😀ak< n>ak< n>", false)]
    [InlineData(@"(?<n>a)\k< n>", "😀ak< n>ak< n>", true)]
    [InlineData(@"\k<x+>", "😀k<xx>k<x>", false)]
    [InlineData(@"\k<x+>", "😀k<xx>k<x>", true)]
    public void EcmaLexicalFallbacksAreOwnedGrammar(string pattern, string text, bool compiled)
    {
        var options = RegexOptions.CultureInvariant | RegexOptions.ECMAScript |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var tree = Utf8OwnedTreeParser.TryParse(pattern, options);
        Assert.NotNull(tree);
        Assert.NotNull(Utf8SliceParser.TryParse(options, tree));
        var regex = new Utf8Regex(pattern, options);
        var oracle = new Regex(pattern, options);
        Utf8SliceSparseGroupTests.AssertOwnedOperations(regex, oracle, [text, "", "😀", "a", "-", "[", "a]", "[]", "-]"]);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData(@"\p")]
    [InlineData(@"\pL")]
    [InlineData(@"\p{}")]
    [InlineData(@"\p{L }")]
    [InlineData(@"\p{Greek}")]
    [InlineData(@"[\p]")]
    [InlineData(@"[\P{xx}]")]
    public void EcmaDoesNotTreatInvalidPropertiesAsIdentityEscapes(string pattern)
    {
        var options = RegexOptions.ECMAScript | RegexOptions.CultureInvariant;
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, options));
        Assert.Throws<RegexParseException>(() => new Regex(pattern, options));
        Utf8PatternFailureAssert.Throws(() => new Utf8Regex(pattern, options));
    }

    [Theory]
    [InlineData("*a", "MissingQuantifierOperand", 0)]
    [InlineData("a**", "MissingQuantifierOperand", 2)]
    [InlineData("a{3,2}", "InvalidQuantifier", 5)]
    [InlineData("a)", "UnmatchedClosingParenthesis", 1)]
    [InlineData("(a", "UnclosedGroup", 2)]
    [InlineData("[a", "UnclosedClass", 2)]
    [InlineData("[z-a]", "ReversedRange", 4)]
    [InlineData("[🙏-😀]", "ReversedRange", 6)]
    [InlineData("(?(?i)a|b)", "InvalidConditional", 3)]
    [InlineData("(?(1 )a|b)(a)", "InvalidConditional", 4)]
    [InlineData("(a)(?(1)b|c|d)", "InvalidConditional", 11)]
    [InlineData(@"\q", "InvalidEscape", 1)]
    [InlineData(@"\p{Greek}", "InvalidProperty", 0)]
    [InlineData("(?<0>a)", "InvalidCaptureName", 3)]
    [InlineData("(?<1a>b)", "InvalidCaptureName", 3)]
    [InlineData(@"\k<missing>", "UndefinedCapture", 11)]
    [InlineData("😀(", "UnclosedGroup", 3)]
    [InlineData("😀\\", "InvalidEscape", 3)]
    [InlineData("😀\\k<missing>", "UndefinedCapture", 13)]
    public void OwnedSyntaxDiagnosticsHaveExplicitReasonsAndPatternOffsets(string pattern, string reason, int offset)
    {
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, options, out var diagnostic));
            Assert.True(diagnostic.IsSyntaxError);
            Assert.Equal(reason, diagnostic.Error.ToString());
            Assert.Equal(offset, diagnostic.Offset);
            var failure = Utf8PatternFailureAssert.Throws(() => new Utf8Regex(pattern, options));
            Assert.StartsWith(diagnostic.Description, failure.Message, StringComparison.Ordinal);
            Assert.Contains($"pattern offset {offset}.", failure.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("😀(")]
    [InlineData("[z-a]")]
    [InlineData("[🙏-😀]")]
    [InlineData(@"\p{Greek}")]
    [InlineData(@"(?<x>a)\k<missing>")]
    [InlineData("(a)(?(1)b|c|d)")]
    public void PublicSyntaxFailureRestoresParentAdmissionAndSameOwnerRecovers(string pattern)
    {
        foreach (var compiled in new[] { false, true })
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, 65536, ulong.MaxValue, 128);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            Utf8PatternFailureAssert.Throws(() => new Utf8Regex(pattern, options));
            Assert.Equal(112ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            var spent = owner.Budget.WorkSteps;
            Assert.True(spent > 2);
            using (var recovery = Utf8ConstructionScope.Enter())
            {
                var regex = new Utf8Regex(@"(?<x>😀)\k<x>", options);
                Assert.Equal(new[] { "0", "x" }, regex.GetGroupNames());
                Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
                Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
            }
            Assert.Equal(112ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            Assert.True(owner.Budget.WorkSteps > spent);
            Assert.Equal(17, parent.Value);
        }
    }
}
