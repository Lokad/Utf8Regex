using System.Text;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReTranslationContinuationTests
{
    [Theory]
    [InlineData(4097, "(?:")] [InlineData(16385, "(?:")]
    [InlineData(4097, "(")] [InlineData(16385, "(")]
    [InlineData(4097, "(?>")] [InlineData(16385, "(?>")]
    [InlineData(4097, "(?s:")] [InlineData(16385, "(?s:")]
    public void PublicDeepConstructorsAndFullMatchesRemainOwnedAndScalar(int depth, string opener)
    {
        // Atomic syntax uses additional owned parser frames. Exercise the
        // explicit shared owner here and keep the default denial separately.
        using var construction = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity,
            Utf8ConstructionScope.DefaultBufferBytes, Utf8ConstructionScope.DefaultWorkSteps, 131072);
        var pattern = string.Concat(Enumerable.Repeat(opener, depth)) + "." + new string(')', depth);
        var regex = new Utf8PythonRegex(pattern);
        var result = regex.FullMatchDetailedData("😀"u8);
        Assert.True(result.Success);
        Assert.Equal((0, 4), (result.Value.StartOffsetInBytes, result.Value.EndOffsetInBytes));
        if (opener == "(")
        {
            Assert.Equal(depth + 1, result.Groups.Length);
            Assert.Equal("😀", result.Groups[depth].ValueText);
        }
        Assert.InRange(construction.Budget.PeakDepth, 1ul, 131072ul);
        Assert.Equal(0ul, construction.Budget.Depth);
    }

    [Fact]
    public void DefaultDepthStillRejectsAtomicExpansionAndRestoresTheEnclosingOwner()
    {
        const int depth = 16385;
        var pattern = string.Concat(Enumerable.Repeat("(?>", depth)) + "." + new string(')', depth);
        using var construction = Utf8ConstructionScope.Enter();
        var parent = construction.Budget.AllocateArray<byte>(1, false);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8PythonRegex(pattern));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal(65536ul, failure.Limit);
        Assert.Equal(65537ul, failure.Requested);
        Assert.Equal(48ul, construction.Budget.ReservedBytes);
        Assert.Equal(0ul, construction.Budget.Depth);
        Assert.Equal(0ul, construction.Budget.Reserved);
        Assert.True(new Utf8PythonRegex(".").IsMatch("😀"u8));
        construction.Budget.ReleaseArray(parent, false);
    }

    [Theory]
    [InlineData(4097)] [InlineData(16385)]
    public void DeepLookbehindMeasuresSupplementaryContentAsOneScalar(int depth)
    {
        var inner = string.Concat(Enumerable.Repeat("(?:", depth)) + "😀" + new string(')', depth);
        var regex = new Utf8PythonRegex("(?<=" + inner + ")(?P<x>.)");
        var result = regex.SearchDetailedData("😀é"u8);
        Assert.True(result.Success);
        Assert.Equal((4, 6), (result.Value.StartOffsetInBytes, result.Value.EndOffsetInBytes));
        Assert.Equal("é", result.Groups[1].ValueText);
    }

    [Theory]
    [InlineData(4097)] [InlineData(16385)]
    public void NestedLiteralExtractionKeepsTheWholeLiteralAndFastPathDecisions(int depth)
    {
        var pattern = string.Concat(Enumerable.Repeat("(?:", depth)) + "éab" + new string(')', depth);
        var parse = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
        Assert.True(PythonReTranslator.TryGetCaseSensitiveExactLiteral(parse.Root, parse.Options, out var literal));
        Assert.Equal("éab"u8.ToArray(), literal);
        Assert.True(PythonReTranslator.CanUseUtf8IterationFastPath(parse.Root));
        var regex = new Utf8PythonRegex(pattern);
        Assert.Equal("éab", regex.SearchToString("xéabz"u8));
    }

    public static IEnumerable<object[]> Walkers()
    {
        foreach (var operation in Enumerable.Range(0, 7)) yield return [operation];
    }

    [Theory]
    [MemberData(nameof(Walkers))]
    public void EveryRecursivePredecessorNowInheritsTheDepthLimitAndPreservesParentLoans(int operation)
    {
        var parse = new PythonReParser(string.Concat(Enumerable.Repeat("(?:", 129)) + "a" + new string(')', 129)).Parse(PythonReCompileOptions.None);
        using var scope = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = scope.Budget.AllocateArray<byte>(1, false);
        scope.Budget.EnterDepth();
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(operation, parse));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal(8ul, failure.Limit);
        Assert.Equal(9ul, failure.Requested);
        Assert.Equal(1ul, scope.Budget.Depth);
        Assert.Equal(48ul, scope.Budget.ReservedBytes);
        Assert.True(scope.Budget.WorkSteps > 1);
        var simple = new PythonReParser("a").Parse(PythonReCompileOptions.None);
        var held = scope.Budget.ReservedBytes;
        Assert.True(held >= 48ul + 128ul);
        _ = Run(operation, simple);
        Assert.True(scope.Budget.ReservedBytes >= 48ul
            + (operation == 5 ? Utf8ConstructionBudget.ArrayBytes<int>(1) : 0));
        Assert.True(scope.Budget.ReservedBytes >= held);
        Assert.Equal(1ul, scope.Budget.Depth);
        scope.Budget.LeaveDepth();
        scope.Budget.ReleaseArray(parent, false);
    }

    [Theory]
    [MemberData(nameof(Walkers))]
    public void EverySmallWorkCutoffReleasesTraversalAndMemoCapacity(int operation)
    {
        var parse = new PythonReParser("(?:ab)").Parse(PythonReCompileOptions.None);
        ulong work;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            _ = Run(operation, parse);
            work = measured.Budget.WorkSteps;
        }
        Assert.InRange(work, 1ul, 512ul);
        for (var cutoff = 0ul; cutoff <= work; cutoff++)
        {
            using var scope = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, cutoff + 1, ulong.MaxValue);
            var parent = scope.Budget.AllocateArray<byte>(1, false);
            scope.Budget.EnterDepth();
            var failure = Record.Exception(() => Run(operation, parse));
            Assert.Equal(1ul, scope.Budget.Depth);
            if (cutoff == work)
            {
                Assert.Null(failure);
                Assert.True(scope.Budget.ReservedBytes >= 48ul
            + (operation == 5 ? Utf8ConstructionBudget.ArrayBytes<int>(1) : 0));
            }
            else
            {
                var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, breach.ResourceKind);
                Assert.True(breach.Requested > breach.Limit);
                Assert.Equal(48ul, scope.Budget.ReservedBytes);

            }
            scope.Budget.LeaveDepth();
            scope.Budget.ReleaseArray(parent, false);
        }
    }

    [Theory]
    [InlineData("(?:a|b)", true)] [InlineData("(?:a|ab)", false)]
    [InlineData("(a)?", true)] [InlineData("(?=(a))a", true)]
    [InlineData("(?:a)+", true)] [InlineData("(a)+", true)]
    [InlineData("(a)(?(1)b|c)", false)]
    public void DeepIterationDecisionsKeepTheirConservativeRules(string fragment, bool iterationSafe)
    {
        const int depth = 4097;
        var pattern = string.Concat(Enumerable.Repeat("(?:", depth)) + fragment + new string(')', depth);
        var parse = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
        Assert.Equal(iterationSafe, PythonReTranslator.CanUseUtf8IterationFastPath(parse.Root));
        var simple = new PythonReParser(fragment).Parse(PythonReCompileOptions.None);
        Assert.Equal(PythonReTranslator.CanUseUtf8IterationFastPath(simple.Root), PythonReTranslator.CanUseUtf8IterationFastPath(parse.Root));
    }

    [Theory]
    [InlineData("(?P=missing)", "unknown group name")]
    [InlineData("(?(2)a|b)", "invalid group reference")]
    [InlineData("(?<=a*)b", "look-behind requires fixed-width pattern")]
    [InlineData("(?<=a|bc)b", "look-behind requires fixed-width pattern")]
    public void DeepTranslationErrorsRestoreParentCapacityAndRetainProfileMessages(string fragment, string message)
    {
        const int depth = 16385;
        // Unknown names now fail during parsing. Build a valid deep reference,
        // then remove its definition metadata to exercise the translator's
        // independent invalid-AST check and unwind as before.
        var parsedFragment = fragment == "(?P=missing)" ? "(?P<missing>a)(?P=missing)" : fragment;
        var parse = new PythonReParser(string.Concat(Enumerable.Repeat("(?:", depth)) + parsedFragment + new string(')', depth)).Parse(PythonReCompileOptions.None);
        if (fragment == "(?P=missing)")
            parse = parse with { NamedGroups = new Dictionary<string, int>() };
        using var scope = Utf8ConstructionScope.Enter();
        var parent = scope.Budget.AllocateArray<byte>(1, false);
        Assert.StartsWith(message, Assert.Throws<PythonRePatternException>(() => PythonReTranslator.Translate(parse, true)).Message);
        Assert.Equal(0ul, scope.Budget.Depth);
        Assert.Equal(48ul, scope.Budget.ReservedBytes);
        Assert.True(new Utf8PythonRegex(".").IsMatch("😀"u8));
        scope.Budget.ReleaseArray(parent, false);
    }

    [Theory]
    [InlineData(32)] [InlineData(128)]
    public void SharedBackreferenceWidthsAreMemoizedWithoutRecursiveRecordHashing(int groups)
    {
        var pattern = new StringBuilder("(?P<g0>a)");
        for (var number = 1; number < groups; number++)
            pattern.Append("(?P<g").Append(number).Append(">(?P=g").Append(number - 1).Append(")(?P=g").Append(number - 1).Append("))");
        var parse = new PythonReParser(pattern.ToString()).Parse(PythonReCompileOptions.None);
        using var scope = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 100_000, ulong.MaxValue);
        Assert.False(PythonReTranslator.CanMatchEmpty(parse.Root, parse));
        Assert.True(PythonReTranslator.GetWidth(parse.Root, parse).Max > 0);
        Assert.Equal(0ul, scope.Budget.ReservedBytes);
        Assert.Equal(0ul, scope.Budget.Depth);
        Assert.InRange(scope.Budget.WorkSteps, 1ul, 100_000ul);
    }

    [Fact]
    public void SaturatedAlternationMinimumDoesNotBecomeAnEmptyPattern()
    {
        var parse = new PythonReParser("a{2147483647}|b{2147483647}").Parse(PythonReCompileOptions.None);
        Assert.False(PythonReTranslator.CanMatchEmpty(parse.Root, parse));
        Assert.True(PythonReTranslator.GetWidth(parse.Root, parse).Max > 0);
        Assert.Equal("(?<=a{2147483647}|b{2147483647})"u8.ToArray(), PythonReTranslator.Translate(
            new PythonReParser("(?<=a{2147483647}|b{2147483647})").Parse(PythonReCompileOptions.None), true).NativePatternUtf8);
    }

    [Theory]
    [InlineData("(?<=a{2147483647}b)x", null)]
    [InlineData("(?<=(?:)*)x", null)]
    [InlineData("(?<=😀{2147483647}é)x", null)]
    [InlineData("(?<=😀|ab)x", "look-behind requires fixed-width pattern")]
    [InlineData("(?P<x>😀|ab)(?<=(?P=x))x", "look-behind requires fixed-width pattern")]
    [InlineData("(?<=a{2147483647}|a{2147483646}bb)x", "look-behind requires fixed-width pattern")]
    [InlineData("(?<=a{2147483647}a{2147483647}bb)x", "looks too much behind")]
    public void WideScalarLookbehindBoundsPreserveExactFixednessAndPythonErrors(string pattern, string? error)
    {
        var parse = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
        if (error is null)
        {
            var native = PythonReTranslator.Translate(parse, true).NativePatternUtf8;
            Assert.True(native.AsSpan().EndsWith(")x"u8));
        }
        else Assert.StartsWith(error, Assert.Throws<PythonRePatternException>(() => PythonReTranslator.Translate(parse, true)).Message);
    }

    [Fact]
    public void PureEmptyUnboundedRepetitionHasNoNonEmptyMatchAndKeepsScalarEmptyProgress()
    {
        var parse = new PythonReParser("(?:)*").Parse(PythonReCompileOptions.None);
        Assert.True(PythonReTranslator.CanMatchEmpty(parse.Root, parse));
        Assert.False(PythonReTranslator.GetWidth(parse.Root, parse).Max > 0);
        var regex = new Utf8PythonRegex("(?<=(?:)*)x");
        Assert.Equal("x", regex.SearchToString("😀x"u8));
    }

    private static object Run(int operation, PythonReParseResult parse) => operation switch
    {
        0 => PythonReTranslator.CanMatchEmpty(parse.Root, parse),
        1 => PythonReTranslator.GetWidth(parse.Root, parse).Max > 0,
        2 => PythonReTranslator.CanUseUtf8IterationFastPath(parse.Root),
        3 => PythonReTranslator.GetWidth(parse.Root, parse),
        4 => PythonReTranslator.IsExactLiteral(parse.Root),
        5 => PythonReTranslator.Translate(parse, true),
        _ => PythonReTranslator.RenderNativeAtom(parse.Root),
    };
}
