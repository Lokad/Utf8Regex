using System.Text;
using System.Text.Json;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Posix.Tests;

public sealed class PosixParserTests
{
    private static JsonDocument Fixtures() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Corpus", "selection.json")));

    [Fact]
    public void IndependentSelectionPatternsAreAdmittedAndLoweredWithoutAnExecutionClaim()
    {
        using var fixtures = Fixtures();
        foreach (var family in new[] { "cases", "iterationCases" })
        foreach (var row in fixtures.RootElement.GetProperty(family).EnumerateArray())
        {
            var options = row.TryGetProperty("options", out var flag)
                ? Enum.Parse<PosixCompileOptions>(flag.GetString()!) : default;
            var root = PosixParser.Parse(row.GetProperty("pattern").GetString()!, options, out var groups);
            var program = Utf8SliceLowerer.Lower(root, null, false, false, false);
            Assert.NotNull(program);
            Assert.Equal(groups == 0 ? 0 : groups + 1, program.GroupCount);
            Assert.False(program.RightToLeft);
            Assert.False(program.IgnoreCase);
            Assert.Empty(program.Assertions);
        }
    }

    [Fact]
    public void IndependentUnsupportedPatternsFailAtAdmission()
    {
        using var fixtures = Fixtures();
        foreach (var pattern in fixtures.RootElement.GetProperty("rejectedPatterns").EnumerateArray())
            Assert.Throws<ArgumentException>(() => PosixParser.Parse(pattern.GetString()!, default, out _));
    }

    // These unambiguous atom/language checks exercise the production parser and
    // shared lowerer. Ordered execution is deliberately not a POSIX winner oracle.
    [Theory]
    [InlineData("[a-z]+", "abc", false, true)]
    [InlineData("[a-z]+", "ABC", false, false)]
    [InlineData("[é-ê]", "é", false, true)]
    [InlineData("[é-ê]", "😀", false, false)]
    [InlineData("[😀-🙏]", "😀", false, true)]
    [InlineData("[^a]", "😀", false, true)]
    [InlineData("[^a]", "a", false, false)]
    [InlineData("[^a]", "\n", false, true)]
    [InlineData("[^a]", "\n", true, false)]
    [InlineData(".", "\n", false, true)]
    [InlineData(".", "\n", true, false)]
    [InlineData("^a$", "a\n", false, false)]
    [InlineData("^a$", "a\n", true, true)]
    [InlineData("^a$", "ba", true, false)]
    [InlineData("\\(\\)", "()", false, true)]
    [InlineData("[]-]", "]", false, true)]
    [InlineData("[]-]", "-", false, true)]
    [InlineData("[-a]", "-", false, true)]
    [InlineData("[\\]", "\\", false, true)]
    [InlineData("a{0}", "", false, true)]
    [InlineData("a{2,3}", "aa", false, true)]
    [InlineData("a{2,3}", "a", false, false)]
    public void ScalarAtomsAndEreOperatorsRetainTheirAdmittedLanguage(string pattern, string input, bool newline, bool expected)
    {
        var root = PosixParser.Parse(pattern, newline ? PosixCompileOptions.Newline : default, out var groups);
        var program = Utf8SliceLowerer.Lower(root, null, false, false, false)!;
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        var result = Utf8SliceRunner.TryMatchAt(program, Encoding.UTF8.GetBytes(input), 0,
            new int[groups + 1], new int[groups + 1], true, ref invocation);
        Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
        Assert.Equal(expected, result.IsMatch);
    }

    [Theory]
    [InlineData("alnum", "Az09", "_é😀")]
    [InlineData("alpha", "Az", "09_é😀")]
    [InlineData("blank", "\t ", "\n\ré")]
    [InlineData("cntrl", "\0\t\n\u001f\u007f", " Azé")]
    [InlineData("digit", "09", "az²")]
    [InlineData("graph", "!Az09~", "\t \u007fé")]
    [InlineData("lower", "az", "AZé")]
    [InlineData("print", " !~", "\t\u007fé")]
    [InlineData("punct", "!_~", "Az09 é")]
    [InlineData("space", "\t\n\v\f\r ", "\0A\u0085")]
    [InlineData("upper", "AZ", "azÉ")]
    [InlineData("xdigit", "09aAfF", "gGé")]
    public void NamedClassesHaveFixedAsciiMembership(string name, string accepted, string rejected)
    {
        foreach (var scalar in accepted.EnumerateRunes()) Check(scalar, true);
        foreach (var scalar in rejected.EnumerateRunes()) Check(scalar, false);
        void Check(Rune scalar, bool expected) => ScalarAtomsAndEreOperatorsRetainTheirAdmittedLanguage(
            $"[[:{name}:]]", scalar.ToString(), false, expected);
    }

    [Theory]
    [InlineData("[a-b-c]")]
    [InlineData("[a-[:digit:]]")]
    [InlineData("[[:digit:]-z]")]
    [InlineData("a{,2}")]
    [InlineData("a{1,256}")]
    [InlineData("a**")]
    [InlineData("$+")]
    [InlineData("a}")]
    [InlineData("[😀-é]")]
    public void InvalidRangesAndRepetitionsFail(string pattern)
        => Assert.Throws<ArgumentException>(() => PosixParser.Parse(pattern, default, out _));

    [Fact]
    public void MalformedPatternScalarsAndUnknownOptionsFail()
    {
        foreach (var input in new[] { "\ud800", "[\udc00]", "abc\ud800" })
            Assert.Throws<ArgumentException>(() => PosixParser.Parse(input, default, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => PosixParser.Parse("a", (PosixCompileOptions)2, out _));
    }

    [Fact]
    public void NestedGroupsUseAdmittedFramesRatherThanRecursiveParsing()
    {
        const int depth = 4096;
        var root = PosixParser.Parse(new string('(', depth) + "😀" + new string(')', depth), default, out var groups);
        Assert.Equal(depth, groups);
        for (var slot = 1; slot <= depth; slot++)
        {
            var group = Assert.IsType<Utf8SliceNode.CaptureNode>(root);
            Assert.Equal(slot, group.Slot); root = group.Body;
        }
        Assert.Equal(0x1f600, Assert.IsType<Utf8SliceNode.LiteralNode>(root).Value);
    }

    [Theory]
    [InlineData(Utf8ResourceLimitKind.ConstructionDepth)]
    [InlineData(Utf8ResourceLimitKind.ConstructionBytes)]
    [InlineData(Utf8ResourceLimitKind.ConstructionWorkSteps)]
    public void FailureRestoresParentCapacityAndDepthWithoutRefundingWork(Utf8ResourceLimitKind kind)
    {
        using var scope = new Utf8ConstructionScope(1024,
            kind == Utf8ResourceLimitKind.ConstructionBytes ? 128UL : 1UL << 20,
            kind == Utf8ResourceLimitKind.ConstructionWorkSteps ? 3UL : 1UL << 20,
            kind == Utf8ResourceLimitKind.ConstructionDepth ? 2UL : 100);
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => PosixParser.Parse("(((abcdef)))", default, out _));
        Assert.Equal(kind, error.ResourceKind);
        Assert.Equal(0UL, scope.Budget.Depth);
        Assert.Equal(0UL, scope.Budget.ReservedBytes);
        Assert.Equal(0UL, scope.Budget.Reserved);
        if (kind != Utf8ResourceLimitKind.ConstructionBytes) Assert.True(scope.Budget.WorkSteps > 0);
    }

    [Fact]
    public void CancellationDuringAdmissionKeepsTheOriginalTokenAndRestoresTheParent()
    {
        using var canceled = new CancellationTokenSource();
        using var scope = Utf8ConstructionScope.Enter(default, Timeout.InfiniteTimeSpan, canceled.Token);
        scope.Budget.CheckpointHook = steps => { if (steps >= 32) canceled.Cancel(); };
        var error = Assert.Throws<OperationCanceledException>(() => PosixParser.Parse(new string('a', 2048), default, out _));
        Assert.Equal(canceled.Token, error.CancellationToken);
        Assert.Equal(0UL, scope.Budget.Depth);
        Assert.Equal(0UL, scope.Budget.ReservedBytes);
    }
}
