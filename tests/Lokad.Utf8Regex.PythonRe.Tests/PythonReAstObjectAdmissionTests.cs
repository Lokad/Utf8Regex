using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReAstObjectAdmissionTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)] [InlineData(12)]
    public void EachAstObjectAndItsTrackingDescriptorFitTheirAcquiredFieldStateLoan(int kind)
    {
        using var owner = Utf8ConstructionScope.Enter();
        _ = Create(kind); // Warm generic factory and shared metadata before measurement.
        var previous = owner.Budget.ObjectCheckpoint;
        var held = owner.Budget.ReservedBytes;
        var before = GC.GetAllocatedBytesForCurrentThread();
        var node = Create(kind);
        var allocated = (ulong)(GC.GetAllocatedBytesForCurrentThread() - before);
        var acquired = Assert.IsType<Utf8ConstructionBudget.ObjectAllocation>(owner.Budget.ObjectCheckpoint);
        Assert.Same(node, acquired.Value);
        Assert.Same(previous, acquired.Previous);
        Assert.True(allocated > 0);
        Assert.True(allocated <= acquired.Bytes, $"{node.GetType().Name}: {allocated} allocated / {acquired.Bytes} admitted");
        Assert.Equal(held + acquired.Bytes, owner.Budget.ReservedBytes);
    }

    [Theory]
    [InlineData("a|b")]
    [InlineData("(?P<x>😀)(?(x)\\1|(?i:[a-z]))")]
    [InlineData("(?s:(?>a+))(?=b)(?<=a)[é-🙏]")]
    [InlineData("[\\w\\D]\\s")]
    public void SuccessfulParserHoldsEveryAcquiredAstLoanUntilItsOwnerCloses(string pattern)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var parsed = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
        Assert.NotNull(parsed.Root);
        var bytes = 0ul;
        var count = 0;
        for (var allocation = owner.Budget.ObjectCheckpoint; allocation is not null; allocation = allocation.Previous)
        {
            Assert.True(allocation.Value is PythonReNode or PythonReCharacterClassItem);
            bytes += allocation.Bytes;
            count++;
        }
        Assert.True(count > 0);
        Assert.Equal(bytes, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Equal(0ul, owner.Budget.Depth);
    }

    [Theory]
    [InlineData("abcdef")]
    [InlineData("(?P<x>a)(?(x)b|c)")]
    [InlineData("(?i:[a-c😀])")]
    public void EveryParserByteTransitionPreservesParentAstObjectsAndAdmitsAtTheMeasuredPeak(string pattern)
    {
        ulong peak;
        using (var measured = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            _ = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
            peak = measured.Budget.PeakBytes;
        }
        Assert.InRange(peak, 112ul, 65536ul);
        for (var cutoff = 0ul; cutoff <= peak; cutoff += 16)
        foreach (var limit in new[] { cutoff, cutoff == 0 ? 0 : cutoff - 1 })
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, limit + 112, ulong.MaxValue, ulong.MaxValue);
            var parent = PythonReAstFactory.Literal('p');
            var checkpoint = owner.Budget.ObjectCheckpoint;
            var work = owner.Budget.WorkSteps;
            var failure = Record.Exception(() => new PythonReParser(pattern).Parse(PythonReCompileOptions.None));
            Assert.True(owner.Budget.PeakBytes <= limit + 112);
            Assert.Equal(0ul, owner.Budget.Depth);
            if (failure is not null)
            {
                var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, breach.ResourceKind);
                Assert.True(breach.Requested > breach.Limit);
                Assert.Equal(112ul, owner.Budget.ReservedBytes);
                Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
                Assert.Same(parent, checkpoint?.Value);
            }
            else
            {
                Assert.Null(failure);
                Assert.True(owner.Budget.ReservedBytes > 112);
            }
            if (limit == peak) Assert.Null(failure);
            Assert.True(owner.Budget.WorkSteps >= work);
        }
    }

    [Theory]
    [InlineData("abcdef", 256ul)]
    [InlineData("(?:a|b){2}", 512ul)]
    public void DeniedChildParserCanBeFollowedByScalarConstructionInTheSameOwner(string pattern, ulong allowance)
    {
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, allowance + 112, ulong.MaxValue, ulong.MaxValue);
        var parent = PythonReAstFactory.Literal('p');
        var checkpoint = owner.Budget.ObjectCheckpoint;
        Assert.Throws<Utf8ResourceLimitExceededException>(() => new PythonReParser(pattern).Parse(PythonReCompileOptions.None));
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        var spent = owner.Budget.WorkSteps;
        var parsed = new PythonReParser("😀").Parse(PythonReCompileOptions.None);
        Assert.Equal("😀", Assert.IsType<PythonReRawNode>(parsed.Root).Text);
        Assert.Same(parent, checkpoint?.Value);
        Assert.True(owner.Budget.WorkSteps > spent);
    }

    private static object Create(int kind) => kind switch
    {
        0 => PythonReAstFactory.Alternation([]),
        1 => PythonReAstFactory.Sequence([]),
        2 => PythonReAstFactory.Literal('é'),
        3 => PythonReAstFactory.Raw("😀", PythonReRawKind.AstralLiteral),
        4 => PythonReAstFactory.CharacterClass(true, []),
        5 => PythonReAstFactory.Quantifier(PythonReAstFactory.Empty, 1, null, PythonReQuantifierFlavor.Possessive),
        6 => PythonReAstFactory.Group(PythonReGroupKind.NamedCapturing, PythonReAstFactory.Empty, "x",
            PythonReCompileOptions.DotAll, PythonReCompileOptions.Multiline, 1),
        7 => PythonReAstFactory.NamedReference("x", static value => value),
        8 => PythonReAstFactory.NumericReference(1, static value => value),
        9 => PythonReAstFactory.Conditional(PythonReConditionalReference.ForGroupName("x"), PythonReAstFactory.Empty, null),
        10 => PythonReAstFactory.ClassLiteral(0x1f600),
        11 => PythonReAstFactory.ClassRange(0x1f600, 0x1f64f),
        12 => PythonReAstFactory.ClassPredefined("\\w", true),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
