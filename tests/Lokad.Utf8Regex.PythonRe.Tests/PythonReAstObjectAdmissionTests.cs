using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReAstObjectAdmissionTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)] [InlineData(12)]
    public void EachAstObjectFitsItsConservativeAcquisitionCharge(int kind)
    {
        using var owner = Utf8ConstructionScope.Enter();
        _ = Create(kind); // Warm generic factory and shared metadata before measurement.
        var previous = owner.Budget.ReservedBytes;
        var held = owner.Budget.ReservedBytes;
        var before = GC.GetAllocatedBytesForCurrentThread();
        var node = Create(kind);
        var allocated = (ulong)(GC.GetAllocatedBytesForCurrentThread() - before);
        var charge = owner.Budget.ReservedBytes - held;
        Assert.InRange(charge, 128ul, ulong.MaxValue);
        Assert.InRange(allocated, 1ul, charge);
        Assert.NotNull(node);

    }

    [Theory]
    [InlineData("a|b")]
    [InlineData("(?P<x>😀)(?(x)\\1|(?i:[a-z]))")]
    [InlineData("(?s:(?>a+))(?=b)(?<=a)[é-🙏]")]
    [InlineData("[\\w\\D]\\s")]
    public void SuccessfulParserRetainsItsReturnedRootAndConservativeCapacity(string pattern)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var parsed = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
        Assert.NotNull(parsed.Root);
        Assert.True(owner.Budget.ReservedBytes >= 128ul);
        // The returned root, metadata and arrays carry their own references;
        // the admission owner no longer retains a second object graph.
        GC.KeepAlive(parsed);
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
        Assert.InRange(peak, 128ul, 65536ul);
        for (var cutoff = 0ul; cutoff <= peak; cutoff += 16)
        foreach (var limit in new[] { cutoff, cutoff == 0 ? 0 : cutoff - 1 })
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, limit + 128, ulong.MaxValue, ulong.MaxValue);
            var parent = PythonReAstFactory.Literal('p');
            var checkpoint = owner.Budget.ReservedBytes;
            var work = owner.Budget.WorkSteps;
            var failure = Record.Exception(() => new PythonReParser(pattern).Parse(PythonReCompileOptions.None));
            Assert.True(owner.Budget.PeakBytes <= limit + 128);
            Assert.Equal(0ul, owner.Budget.Depth);
            if (failure is not null)
            {
                var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, breach.ResourceKind);
                Assert.True(breach.Requested > breach.Limit);
                Assert.Equal(128ul, owner.Budget.ReservedBytes);
                Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
                Assert.NotNull(parent);
            }
            else
            {
                Assert.Null(failure);
                Assert.True(owner.Budget.ReservedBytes > 128);
            }
            if (limit == peak) Assert.Null(failure);
            Assert.True(owner.Budget.WorkSteps >= work);
        }
    }

    [Theory]
    // Include the newly admitted explicit parser frames in the recovery parse.
    [InlineData("abcdef", 1920ul)]
    [InlineData("(?:a|b){2}", 1920ul)]
    public void DeniedChildParserCanBeFollowedByScalarConstructionInTheSameOwner(string pattern, ulong allowance)
    {
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, allowance + 128, ulong.MaxValue, ulong.MaxValue);
        var parent = PythonReAstFactory.Literal('p');
        var checkpoint = owner.Budget.ReservedBytes;
        Assert.Throws<Utf8ResourceLimitExceededException>(() => new PythonReParser(pattern).Parse(PythonReCompileOptions.None));
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        var spent = owner.Budget.WorkSteps;
        var parsed = new PythonReParser("😀").Parse(PythonReCompileOptions.None);
        Assert.Equal("😀", Assert.IsType<PythonReRawNode>(parsed.Root).Text);
        Assert.NotNull(parent);
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
