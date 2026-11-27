using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Posix.Tests;

public sealed class PosixGnuParserTests
{
    [Fact]
    public void BreAndEreSpellingsProduceTheSameReferenceTree()
    {
        foreach (var (pattern, dialect) in new[]
        {
            (@"\(a\|aa\)\1", PosixParser.Dialect.GnuBasic),
            (@"(a|aa)\1", PosixParser.Dialect.GnuExtended),
        })
        {
            var (tree, groups, hasReferences) = PosixParser.Parse(pattern, PosixCompileOptions.IgnoreCase, dialect);
            var root = Assert.IsType<Utf8SliceNode.ConcatNode>(tree);
            Assert.Equal(1, groups);
            Assert.True(hasReferences);
            Assert.IsType<Utf8SliceNode.AlternationNode>(Assert.IsType<Utf8SliceNode.CaptureNode>(root.Children[0]).Body);
            var reference = Assert.IsType<Utf8SliceNode.BackrefNode>(root.Children[1]);
            Assert.Equal(1, reference.Slot);
            Assert.True(reference.FoldCase);
            Assert.False(reference.AllowUnmatched);
        }
    }

    [Theory]
    [InlineData(@"(a\1)")]
    [InlineData(@"\1(a)")]
    [InlineData(@"((a)\1)")]
    [InlineData(@"((a)|b\2)")]
    public void GnuReferenceRequiresAnAlreadyClosedGroup(string pattern)
    {
        var failure = Assert.Throws<ArgumentException>(() => PosixParser.Parse(pattern,
            default, PosixParser.Dialect.GnuExtended));
        Assert.Equal("pattern", failure.ParamName);
    }

    [Fact]
    public void BreIntervalUsesEscapedDelimitersAndOneDigitReferences()
    {
        var root = Assert.IsType<Utf8SliceNode.ConcatNode>(PosixParser.Parse(@"\(a\{1,2\}\)\10",
            default, PosixParser.Dialect.GnuBasic).Root);
        var repeat = Assert.IsType<Utf8SliceNode.RepeatNode>(Assert.IsType<Utf8SliceNode.CaptureNode>(root.Children[0]).Body);
        Assert.Equal((1L, 2L), (repeat.Minimum, repeat.Maximum));
        var suffix = Assert.IsType<Utf8SliceNode.ConcatNode>(root.Children[1]);
        Assert.Equal(1, Assert.IsType<Utf8SliceNode.BackrefNode>(suffix.Children[0]).Slot);
        Assert.IsType<Utf8SliceNode.LiteralNode>(suffix.Children[1]);
    }

    [Fact]
    public void StrictEreStillRejectsTheGnuReferenceExtension()
    {
        Assert.Throws<PosixUnsupportedSyntaxException>(() => PosixParser.Parse(@"(a)\1", default));
    }

    [Theory]
    [InlineData(@"(a)(\1)", 1)]
    [InlineData(@"((a)\2)", 2)]
    public void LoweringPreservesTheReferencedTagAndKeepsCaptureIndependentRoutesOut(string pattern, int slot)
    {
        var program = PosixProgram.Compile(pattern, PosixCompileOptions.IgnoreCase,
            dialect: PosixParser.Dialect.GnuExtended);
        var reference = Assert.Single(program.Scalar.Instructions, x => x.Kind == Utf8SliceInstructionKind.Backref);
        Assert.Equal(program.CaptureTags[slot], reference.A);
        Assert.Equal(1, reference.B);
        Assert.Empty(program.Chart);
        Assert.False(program.FirstAcceptanceIsLongest);
    }
}
