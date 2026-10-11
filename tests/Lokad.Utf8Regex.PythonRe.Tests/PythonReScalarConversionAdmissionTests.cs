using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReScalarConversionAdmissionTests
{
    private static readonly Utf8SliceNode Body = new Utf8SliceNode.EmptyNode();
    private static readonly Utf8SliceNode[] Children = [Body];
    private static readonly Utf8SliceNode[][] Branches = [Children];

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)]
    public void DirectIrObjectFitsConservativeAcquisitionCharge(int kind)
    {
        using var owner = Utf8ConstructionScope.Enter();
        _ = Create(kind);
        var checkpoint = owner.Budget.ReservedBytes;
        var held = owner.Budget.ReservedBytes;
        var before = GC.GetAllocatedBytesForCurrentThread();
        var node = Create(kind);
        var allocated = (ulong)(GC.GetAllocatedBytesForCurrentThread() - before);
        var charge = owner.Budget.ReservedBytes - held;
        Assert.InRange(charge, 128ul, ulong.MaxValue);
        Assert.InRange(allocated, 1ul, charge);
        Assert.NotNull(node);
        if (node is Utf8SliceNode.BackrefNode reference)
        {
            Assert.True(reference.FoldCase);
            Assert.False(reference.AllowUnmatched);
            Assert.NotNull(reference.LowerScalar);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)]
    public void DirectIrFactoryByteAndWorkDenialAcquireNoObject(int kind)
    {
        foreach (var work in new[] { false, true })
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, work ? ulong.MaxValue : 48,
                work ? 1ul : ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateArray<byte>(1, false);
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Create(kind));
            Assert.Equal(work ? Utf8ResourceLimitKind.ConstructionWorkSteps : Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
            Assert.Equal(48ul, owner.Budget.ReservedBytes);
            Assert.Equal(48ul, owner.Budget.PeakBytes);
            Assert.Equal(1ul, owner.Budget.WorkSteps);

            owner.Budget.ReleaseArray(parent, false);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void RootAdmissionPrecedesArenaMapAndLeafAcquisition(int denial)
    {
        var parse = new PythonReParser("(?P<x>.)").Parse(PythonReCompileOptions.None);
        var translation = PythonReTranslator.Translate(parse);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, denial == 0 ? 48ul : ulong.MaxValue,
            denial == 2 ? 1ul : ulong.MaxValue, denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => PythonReScalarCompiler.Compile(parse, translation, false));
        Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes, 1 => Utf8ResourceLimitKind.ConstructionDepth,
            _ => Utf8ResourceLimitKind.ConstructionWorkSteps }, failure.ResourceKind);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(48ul, owner.Budget.PeakBytes);
        Assert.Equal(1ul, owner.Budget.WorkSteps);
        Assert.Equal(0ul, owner.Budget.Depth);

        owner.Budget.ReleaseArray(parent, false);
    }

    [Theory]
    [InlineData("(?P<x>😀)(?(x)é|x)", false)]
    [InlineData("(?P<x>😀)(?(x)é|x)", true)]
    [InlineData("(a)(?P<x>.)(?<=\\1)(?(x)(?:é|b)+|x)", true)]
    public void ConversionByteBoundariesPreserveParentAndAdmitAtTheMeasuredPeak(string pattern, bool full)
    {
        var parse = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
        var translation = PythonReTranslator.Translate(parse);
        ulong peak;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            Assert.NotNull(PythonReScalarCompiler.Compile(parse, translation, full));
            peak = measured.Budget.PeakBytes;
        }
        foreach (var allowance in new[] { 0ul, 128ul, peak / 2, peak - 1, peak })
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, allowance + 128, ulong.MaxValue, ulong.MaxValue);
            var parent = PythonReAstFactory.Literal('p');
            var checkpoint = owner.Budget.ReservedBytes;
            Utf8SliceProgram? program = null;
            var failure = Record.Exception(() => program = PythonReScalarCompiler.Compile(parse, translation, full));
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.True(owner.Budget.PeakBytes <= allowance + 128);
            if (failure is not null)
            {
                Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                Assert.Equal(128ul, owner.Budget.ReservedBytes);
                Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
                Assert.Equal((int)'p', parent.Value);
            }
            else
            {
                Assert.NotNull(program);
                Assert.True(owner.Budget.ReservedBytes > 128);
            }
            if (allowance == peak) Assert.Null(failure);
        }
    }

    [Fact]
    public void InvalidInternalAtomRollsBackItsObjectsAndRetainsSpentWork()
    {
        var parse = new PythonReParser("").Parse(PythonReCompileOptions.None)
            with { Root = new PythonReRawNode(@"\C", PythonReRawKind.Escape) };
        var translation = PythonReTranslator.Translate(parse);
        using var owner = Utf8ConstructionScope.Enter();
        var parent = PythonReAstFactory.Literal('p');
        var checkpoint = owner.Budget.ReservedBytes;
        Assert.Throws<InvalidOperationException>(() => PythonReScalarCompiler.Compile(parse, translation, false));
        Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(@"\C"));
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal((int)'p', parent.Value);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.True(owner.Budget.WorkSteps > 2);
        var recovery = new PythonReParser("(?P<x>.)").Parse(PythonReCompileOptions.None);
        Assert.NotNull(PythonReScalarCompiler.Compile(recovery, PythonReTranslator.Translate(recovery), true));
    }

    [Fact]
    public void NumericCaptureNamesAndDirectIrNodesKeepTheirCompiledBehavior()
    {
        var parse = new PythonReParser("(a)(b)(?P<é>.)").Parse(PythonReCompileOptions.None);
        var translation = PythonReTranslator.Translate(parse);
        using var owner = Utf8ConstructionScope.Enter();
        var program = PythonReScalarCompiler.Compile(parse, translation, true);
        Assert.NotNull(program);
        Assert.Equal(new[] { "0", "1", "2", "g3" }, program.GroupNames);
        Assert.True(owner.Budget.ReservedBytes >= Utf8ConstructionBudget.ArrayBytes<string>(program.GroupNames.Length)
            + Utf8ConstructionBudget.StringBytes(program.GroupNames[1].Length)
            + Utf8ConstructionBudget.StringBytes(program.GroupNames[2].Length));
        // Exercise the actual compiled capture and full-match anchor behavior,
        // instead of requiring temporary IR nodes in an accounting registry.
        Assert.Equal("ab😀", new Utf8PythonRegex("(a)(b)(?P<é>.)").FullMatchToString("ab😀"u8));
        Assert.Equal(0ul, owner.Budget.Depth);
    }

    private static Utf8SliceNode Create(int kind) => kind switch
    {
        0 => PythonReScalarNodeFactory.Concat(Children),
        1 => PythonReScalarNodeFactory.Alternation(Branches),
        2 => PythonReScalarNodeFactory.Repeat(Body, 1, 2, false),
        3 => PythonReScalarNodeFactory.Atomic(Body),
        4 => PythonReScalarNodeFactory.Capture(0, Body),
        5 => PythonReScalarNodeFactory.Lookahead(Body, true),
        6 => PythonReScalarNodeFactory.Lookbehind(Body, false, 1),
        7 => PythonReScalarNodeFactory.Backref(0, static scalar => scalar),
        8 => PythonReScalarNodeFactory.Conditional(0, Body, Body),
        9 => PythonReScalarNodeFactory.Empty(),
        _ => PythonReScalarNodeFactory.VeryEnd(),
    };
}
