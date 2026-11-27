using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SimpleLiteralAdmissionTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static object? Call(Type type, string name, params object?[] args)
    {
        var method = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(value => value.Name == name && value.GetParameters().Length == args.Length);
        try { return method.Invoke(null, args); }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        { ExceptionDispatchInfo.Capture(inner).Throw(); throw; }
    }
    private static object Input(int route) => route switch
    {
        1 => AsciiCharClass.FromBytes("AaBb"u8),
        2 => AsciiCharClass.FromBytes("Aa"u8),
        3 => new AsciiPatternToken[][] { [new AsciiPatternToken(AsciiCharClass.FromBytes("Ab"u8)), new AsciiPatternToken((byte)'!')] },
        _ => Utf8OwnedTreeParser.TryParse("[Aa]x", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)
            ?? throw new InvalidOperationException("Owned parsing failed.")
    };
    private static void Run(int route, object input)
    {
        switch (route)
        {
            case 1:
                object?[] bytesArgs = [input, true, null];
                Assert.True(Assert.IsType<bool>(Call(typeof(Utf8FiniteLiteralLowerer), "TryExtractLiteralBytes", bytesArgs)));
                Assert.Equal("ab"u8.ToArray(), Assert.IsType<byte[]>(bytesArgs[2])); break;
            case 2:
                object?[] foldedArgs = [input, (byte)0];
                Assert.True(Assert.IsType<bool>(Call(typeof(Utf8FiniteLiteralLowerer), "TryExtractIgnoreCaseLiteral", foldedArgs)));
                Assert.Equal((byte)'a', foldedArgs[1]); break;
            case 3:
                Assert.True(Utf8FiniteLiteralLowerer.TryExpandLiteralBranches(Assert.IsType<AsciiPatternToken[][]>(input), false, out var literals));
                Assert.Equal(new[] { "A!", "b!" }, literals.Select(Encoding.ASCII.GetString)); break;
            default:
                var options = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
                var semantic = new Utf8SemanticRegex("[Aa]x", options, 0, options, Assert.IsType<Utf8OwnedRegexTree>(input));
                Assert.True(Utf8NativeExecutionAnalyzer.TryAnalyze(semantic, default, out var analysis));
                Assert.Equal("ax"u8.ToArray(), analysis.LiteralUtf8);
                Assert.Equal(NativeExecutionKind.AsciiLiteralIgnoreCase, analysis.ExecutionKind);
                Assert.Same(analysis.LiteralUtf8, analysis.SearchFacts.LiteralUtf8); break;
        }
    }
    public static IEnumerable<object[]> RootDenials()
    {
        for (var route = 1; route < 5; route++)
        for (var denial = 0; denial < 2; denial++)
            if (denial != 0 || route != 2) yield return [route, denial];
    }
    [Theory]
    [MemberData(nameof(RootDenials))]
    public void RootDenialsRecoverParentBeforeAcquiringOutput(int route, int denial)
    {
        var input = Input(route);
        using var owner = new Utf8ConstructionScope(0, denial == 0 ? 128ul : ulong.MaxValue,
            denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, input));
        Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes, 1 => Utf8ResourceLimitKind.ConstructionDepth}, error.ResourceKind);
        Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(128ul, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(checkpoint, owner.Budget.ReservedBytes); Assert.Equal(17, parent.Value);
    }
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void EveryWorkCutoffRestoresParentAndAcceptedOutputsRetainLoans(int route)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var input = Input(route); ulong work;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue))
        { Run(route, input); work = measure.Budget.DiagnosticCheckpoints; }
        Assert.InRange(work, 1ul, 10000ul);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FailedBranchExpansionCannotMutateBorrowedTokensOrRetainEarlierOutputs(bool overflow)
    {
        var first = new AsciiPatternToken[] { new((byte)'a'), new((byte)'b') };
        var branch = overflow
            ? Enumerable.Repeat(new AsciiPatternToken(AsciiCharClass.FromBytes("ABCD"u8)), 4).ToArray()
            : new AsciiPatternToken[] { new((byte)'a'), AsciiPatternToken.Dot };
        var plan = (AsciiPatternToken[][])[first, branch]; var original = first.ToArray();
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
        Assert.False(Utf8FiniteLiteralLowerer.TryExpandLiteralBranches(plan,false, out var result)); Assert.Empty(result);
        Assert.Equal(original, first); Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes); Assert.Equal(17, parent.Value);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EveryByteCutoffRecoversPartiallyAcquiredExpansion(bool folded)
    {
        var input = (AsciiPatternToken[][])[[new AsciiPatternToken(AsciiCharClass.FromBytes("AaBb"u8)), new AsciiPatternToken((byte)'!')]];
        ulong peak;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue))
        { measure.Budget.AllocateObject(17, static value => new ParentValue(value)); Assert.True(Utf8FiniteLiteralLowerer.TryExpandLiteralBranches(input,folded, out _)); peak = measure.Budget.PeakBytes; }
        var success = false; var partialDenial = false;
        for (var cap = 128ul; cap <= peak; cap += 16)
        {
            using var owner = new Utf8ConstructionScope(0, cap, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
            var error = Record.Exception(() => Assert.True(Utf8FiniteLiteralLowerer.TryExpandLiteralBranches(input,folded, out _)));
            if (error is null) { success = true; Assert.True(owner.Budget.ReservedBytes > 128); }
            else
            {
                Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                partialDenial |= owner.Budget.PeakBytes > 128; Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            }
            Assert.InRange(owner.Budget.PeakBytes, 128ul, cap); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(17, parent.Value);
        }
        Assert.True(success); Assert.True(partialDenial);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FoldedValuesKeepBranchOrderAndExpansionLimits(bool folded)
    {
        var input = (AsciiPatternToken[][])[[new AsciiPatternToken(AsciiCharClass.FromBytes("AaBb"u8)), new AsciiPatternToken((byte)'!')]];
        Assert.True(Utf8FiniteLiteralLowerer.TryExpandLiteralBranches(input,folded, out var result));
        Assert.Equal(folded ? new[] { "a!", "b!" } : new[] { "A!", "B!", "a!", "b!" }, result.Select(Encoding.ASCII.GetString));
        var tooMany = (AsciiPatternToken[][])[Enumerable.Repeat(new AsciiPatternToken(AsciiCharClass.FromBytes("ABCD"u8)), 4).ToArray()];
        Assert.False(Utf8FiniteLiteralLowerer.TryExpandLiteralBranches(tooMany,folded, out _));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MultipleClassesKeepPrefixMajorOrderAndIndependentFinalPayloads(bool folded)
    {
        var suffix = new string('x', 256);
        var tokens = new[] { new AsciiPatternToken(AsciiCharClass.FromBytes(folded ? "AaBb"u8 : "BA"u8)),
            new AsciiPatternToken((byte)'!'), new AsciiPatternToken(AsciiCharClass.FromBytes(folded ? "CcDd"u8 : "21"u8)) }
            .Concat(suffix.Select(value => new AsciiPatternToken((byte)value))).ToArray();
        var original = tokens.ToArray();
        Assert.True(Utf8FiniteLiteralLowerer.TryExpandLiteralBranches((AsciiPatternToken[][])[tokens],folded, out var result));
        Assert.Equal((folded ? new[] { "a!c", "a!d", "b!c", "b!d" } : new[] { "A!1", "A!2", "B!1", "B!2" })
            .Select(prefix => prefix + suffix), result.Select(Encoding.ASCII.GetString));
        result[0][^1] = (byte)'y';
        Assert.All(result.Skip(1), branch => Assert.Equal((byte)'x', branch[^1]));
        Assert.Equal(original, tokens);
    }

    [Fact]
    public void LongLiteralRunsFitBoundedExpansionWithoutAcquiringEveryPrefix()
    {
        var prefix = new string('x', 2049);
        var suffix = new string('y', 2049);
        var tokens = prefix.Select(value => new AsciiPatternToken((byte)value))
            .Append(new AsciiPatternToken(AsciiCharClass.FromBytes("BA"u8)))
            .Concat(suffix.Select(value => new AsciiPatternToken((byte)value))).ToArray();
        var plan = (AsciiPatternToken[][])[tokens];
        using var owner = new Utf8ConstructionScope(1000000, 65536, 100);
        owner.Budget.ReserveBytes(7);
        Assert.True(Utf8FiniteLiteralLowerer.TryExpandLiteralBranches(plan,false, out var result));
        Assert.Equal(new[] { prefix + "A" + suffix, prefix + "B" + suffix }, result.Select(Encoding.ASCII.GetString));
        result[0][^1] = (byte)'z';
        Assert.Equal((byte)'y', result[1][^1]);
        Assert.InRange(owner.Budget.PeakBytes, 7UL, 65536UL);
        Assert.Equal(0UL, owner.Budget.Depth);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WrappedLiteralConstructionAdmitsLinearWork(bool compiled)
    {
        var literal = "A" + new string('x', 3000);
        var regex = Utf8Regex.Compile("(?:" + literal + ")", options:             RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None), policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxDepth = 100,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            },
            ConstructionPolicy = new()
            {
                MaxDepth = 100,
                MaxBytes = 8 << 20,
                MaxInstructionCapacity = 1000000
            },
            ConstructionTimeout = Timeout.InfiniteTimeSpan
        }, cancellationToken: default);
        Assert.Equal(NativeExecutionKind.ExactAsciiLiteral, regex.Inspection.ExecutionKind);
        var input = Encoding.UTF8.GetBytes("😀" + literal + "中");
        Assert.Equal(1, regex.Count(input));
        Assert.Equal(4, regex.Match(input).StartOffsetInBytes);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(literal), (regex.Match(input).EndOffsetInBytes - regex.Match(input).StartOffsetInBytes));
    }
    [Fact]
    public void ExpansionAcceptsExactlySixtyFourBranchesAndAnEmptyLiteral()
    {
        var token = new AsciiPatternToken(AsciiCharClass.FromBytes("DCBA"u8));
        var branch = Enumerable.Repeat(token, 3).ToArray();
        Assert.True(Utf8FiniteLiteralLowerer.TryExpandLiteralBranches((AsciiPatternToken[][])[branch],false, out var result));
        Assert.Equal(64, result.Length);
        Assert.Equal(new[] { "AAA", "AAD", "ABA", "DDD" }, new[] { 0, 3, 4, 63 }.Select(index => Encoding.ASCII.GetString(result[index])));
        Assert.False(Utf8FiniteLiteralLowerer.TryExpandLiteralBranches((AsciiPatternToken[][])[branch, []],false, out var declined));
        Assert.Empty(declined);
        Assert.True(Utf8FiniteLiteralLowerer.TryExpandLiteralBranches((AsciiPatternToken[][])[[]],false, out var empty));
        Assert.Empty(Assert.Single(empty));
        foreach (var unsupported in new[] { AsciiPatternToken.Dot, new AsciiPatternToken(AsciiCharClass.Empty),
            new AsciiPatternToken(new AsciiCharClass(1, 0, true)), new AsciiPatternToken(AsciiCharClass.FromBytes("ABCDE"u8)) })
        {
            Assert.False(Utf8FiniteLiteralLowerer.TryExpandLiteralBranches((AsciiPatternToken[][])[[unsupported]],false, out declined));
            Assert.Empty(declined);
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EveryExpansionCheckpointKeepsOriginalInterruptionAndParentCapacity(bool timeout)
    {
        var input = (AsciiPatternToken[][])[[new AsciiPatternToken(AsciiCharClass.FromBytes("AB"u8)),
            new AsciiPatternToken((byte)'!'), new AsciiPatternToken((byte)'x')]];
        var checkpoints = 0;
        using (var probe = Utf8ConstructionScope.Enter())
        {
            probe.Budget.CheckpointHook = () => checkpoints++;
            Assert.True(Utf8FiniteLiteralLowerer.TryExpandLiteralBranches(input,false, out _));
        }
        for (var target = 1; target <= checkpoints; target++)
        {
            using var owner = Utf8ConstructionScope.Enter();
            owner.Budget.ReserveBytes(7);
            using var token = new CancellationTokenSource();
            long ticks = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
            var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(50));
            owner.Budget.Deadline = deadline; owner.Budget.CancellationToken = token.Token;
            var current = 0;
            owner.Budget.CheckpointHook = () => { if (++current == target) { if (timeout) ticks = 50; else token.Cancel(); } };
            var error = Record.Exception(() => Utf8FiniteLiteralLowerer.TryExpandLiteralBranches(input,false, out _));
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(error);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
            Assert.Same(deadline, owner.Budget.Deadline);
            Assert.Equal(7UL, owner.Budget.ReservedBytes); Assert.Equal(0UL, owner.Budget.Depth);
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LongExpandedLiteralsReachThePublicNativeRouteWithFinitePolicies(bool folded)
    {
        var suffix = new string('x', 512);
        var regex = Utf8Regex.Compile((folded ? "[AaBb]" : "[AB]") + suffix, options:             RegexOptions.CultureInvariant | (folded ? RegexOptions.IgnoreCase : RegexOptions.None), policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxDepth = 100,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            },
            ConstructionPolicy = new()
            {
                MaxDepth = 100,
                MaxBytes = 8 << 20,
                MaxInstructionCapacity = 1000000
            },
            ConstructionTimeout = Timeout.InfiniteTimeSpan
        }, cancellationToken: default);
        Assert.Equal(folded ? NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals : NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        var input = Encoding.UTF8.GetBytes("😀A" + suffix + "中B" + suffix);
        Assert.Equal(2, regex.Count(input));
        Assert.Equal(4, regex.Match(input).StartOffsetInBytes);
        Assert.Equal(513, (regex.Match(input).EndOffsetInBytes - regex.Match(input).StartOffsetInBytes));
    }
    [Theory]
    [InlineData(false, 3000)]
    [InlineData(true, 3000)]
    [InlineData(false, 4097)]
    [InlineData(true, 4097)]
    public void WrappedLiteralKeepsItsAdmittedPublicRoute(bool compiled, int length)
    {
        var suffix = new string('x', length);
        var regex = Utf8Regex.Compile("(?:A" + suffix + ")", options:             RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None), policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxDepth = 100,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            },
            ConstructionPolicy = new()
            {
                MaxDepth = 100,
                MaxBytes = 8 << 20,
                MaxInstructionCapacity = 1000000
            },
            ConstructionTimeout = Timeout.InfiniteTimeSpan
        }, cancellationToken: default);
        // The speculative lowerer admits at most 4096 token cells. Larger
        // patterns keep scalar execution rather than expanding this optimizer.
        Assert.Equal(length == 3000 ? NativeExecutionKind.ExactAsciiLiteral : NativeExecutionKind.FallbackRegex,
            regex.Inspection.ExecutionKind);
        var input = Encoding.UTF8.GetBytes("😀A" + suffix + "中");
        Assert.Equal(1, regex.Count(input));
        Assert.Equal(4, regex.Match(input).StartOffsetInBytes);
        Assert.Equal(suffix.Length + 1, (regex.Match(input).EndOffsetInBytes - regex.Match(input).StartOffsetInBytes));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SingletonAndExpandedClassesKeepOwnedByteOperations(bool compiled)
    {
        foreach (var row in new[] { (Pattern: "[A]x", Flags: RegexOptions.None, Subject: "😀Ax中Ax😀", Replaced: "😀X中X😀"),
            (Pattern: "[Aa]x", Flags: RegexOptions.IgnoreCase, Subject: "😀AX中ax😀", Replaced: "😀X中X😀"),
            (Pattern: "[Aa][Bb]", Flags: RegexOptions.None, Subject: "😀AB中ab😀", Replaced: "😀X中X😀"),
            (Pattern: @"\.", Flags: RegexOptions.None, Subject: "😀.中.😀", Replaced: "😀X中X😀") })
        {
            var regex = new Utf8Regex(row.Pattern, options: RegexOptions.CultureInvariant | row.Flags | (compiled ? RegexOptions.Compiled : RegexOptions.None));
            var input = Encoding.UTF8.GetBytes(row.Subject); Assert.True(regex.IsMatch(input)); Assert.Equal(2, regex.Count(input));
            var match = regex.Match(input); Assert.Equal(4, match.StartOffsetInBytes); Assert.Equal(row.Pattern == @"\." ? 1 : 2, (match.EndOffsetInBytes - match.StartOffsetInBytes));
            Assert.Equal(match.StartOffsetInBytes, Utf8MatchSequence.First(regex.CreateMatchCursor(input)).Value.StartOffsetInBytes);
            var count = 0; foreach (var item in regex.EnumerateMatches(input)) { Assert.Equal((match.EndOffsetInBytes - match.StartOffsetInBytes), (item.EndOffsetInBytes - item.StartOffsetInBytes)); count++; }
            Assert.Equal(2, count); Assert.Equal(row.Replaced, Encoding.UTF8.GetString(regex.Replace(input, "X"u8)));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null); Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        }
    }
}
