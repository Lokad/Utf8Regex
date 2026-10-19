using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SimpleLiteralAndFactAdmissionTests
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
    private static AsciiSimplePatternPlan Plan(AsciiSimplePatternToken[][] branches, bool folded)
        => new(branches, 0, [], [], false, false, false, folded, true,
            default, default, default, default, default, default, default, default, false, false);
    private static Utf8SearchFactData Data() => new()
    {
        LiteralUtf8 = "one"u8.ToArray(), AlternateLiteralsUtf8 = ["two"u8.ToArray()], CanGuideFallbackStarts = true,
        RequiredPrefilterLiteralUtf8 = "three"u8.ToArray(), RequiredPrefilterAlternateLiteralsUtf8 = ["four"u8.ToArray()],
        SecondaryRequiredPrefilterQuotedAsciiSet = Utf8SearchAsciiSet.FromBytes("AB"u8), SecondaryRequiredPrefilterQuotedAsciiLength = 5,
        FixedDistanceSets = [default], TrailingLiteralUtf8 = "five"u8.ToArray(), OrderedWindowLeadingLiteralsUtf8 = ["six"u8.ToArray()],
        OrderedWindowTrailingLiteralUtf8 = "seven"u8.ToArray(), RequiredWindowPrefilters = [default], OrderedWindowMaxGap = 17,
        OrderedWindowSameLine = true, FallbackStartTransform = new(11, Utf8FallbackStartTransformKind.TrimLeadingAsciiWhitespace),
        Distance = 19, MinRequiredLength = 23, ExactRequiredLength = 29, MaxPossibleLength = 31,
        LeadingBoundary = Utf8BoundaryRequirement.Boundary, TrailingBoundary = Utf8BoundaryRequirement.NonBoundary,
    };
    private static object Input(int route) => route switch
    {
        0 => Data(), 1 => "ASCII\0"u8.ToArray(), 2 => AsciiCharClass.FromBytes("AaBb"u8), 3 => AsciiCharClass.FromBytes("Aa"u8),
        4 => Plan([[new AsciiSimplePatternToken(AsciiCharClass.FromBytes("Ab"u8)), new AsciiSimplePatternToken((byte)'!')]], false),
        _ => Utf8OwnedTreeParser.TryParse("[Aa]x", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)
            ?? throw new InvalidOperationException("Owned parsing failed."),
    };
    private static void Run(int route, object input)
    {
        switch (route)
        {
            case 0:
                var data = Assert.IsType<Utf8SearchFactData>(input); var facts = Utf8SearchFacts.Create(Utf8SearchKind.ExactAsciiLiteral, data);
                foreach (var property in typeof(Utf8SearchFactData).GetProperties())
                {
                    var view = typeof(Utf8SearchFacts).GetProperty(property.Name); Assert.NotNull(view);
                    Assert.Equal(property.GetValue(data), view.GetValue(facts));
                }
                break;
            case 1: Assert.Equal("ASCII\0", Utf8ConstructionScope.CurrentBudget.CopyAsciiText(Assert.IsType<byte[]>(input))); break;
            case 2:
                object?[] bytesArgs = [input, true, null];
                Assert.True(Assert.IsType<bool>(Call(typeof(Utf8AsciiSimplePatternLowerer), "TryExtractLiteralBytes", bytesArgs)));
                Assert.Equal("ab"u8.ToArray(), Assert.IsType<byte[]>(bytesArgs[2])); break;
            case 3:
                object?[] foldedArgs = [input, (byte)0];
                Assert.True(Assert.IsType<bool>(Call(typeof(Utf8AsciiSimplePatternLowerer), "TryExtractIgnoreCaseLiteral", foldedArgs)));
                Assert.Equal((byte)'a', foldedArgs[1]); break;
            case 4:
                Assert.True(Utf8AsciiSimplePatternLowerer.TryExtractLiteralBranches(Assert.IsType<AsciiSimplePatternPlan>(input), out var literals));
                Assert.Equal(new[] { "A!", "b!" }, literals.Select(Encoding.ASCII.GetString)); break;
            default:
                var options = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
                var semantic = new Utf8SemanticRegex("[Aa]x", options, "[Aa]x", options, Assert.IsType<Utf8OwnedRegexTree>(input));
                object?[] args = [semantic, "[Aa]x", options, null];
                Assert.True(Assert.IsType<bool>(Call(typeof(Utf8NativeExecutionAnalyzer), "TryAnalyzeSimplePatternTree", args)));
                var analysis = Assert.IsType<Utf8RegexAnalysis>(args[3]); Assert.Equal("ax", analysis.ExecutionPattern);
                Assert.Equal(NativeExecutionKind.AsciiLiteralIgnoreCase, analysis.ExecutionKind);
                Assert.Same(analysis.LiteralUtf8, analysis.SearchFacts.LiteralUtf8); break;
        }
    }
    public static IEnumerable<object[]> RootDenials()
    {
        for (var route = 0; route < 6; route++)
        for (var denial = 0; denial < 3; denial++)
            if ((denial != 1 || route >= 2) && (denial != 0 || route != 3)) yield return [route, denial];
    }
    [Theory]
    [MemberData(nameof(RootDenials))]
    public void RootDenialsRecoverParentBeforeAcquiringOutput(int route, int denial)
    {
        var input = Input(route);
        using var owner = new Utf8ConstructionScope(0, denial == 0 ? 128ul : ulong.MaxValue,
            denial == 2 ? 2ul : ulong.MaxValue, denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, input));
        Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes, 1 => Utf8ResourceLimitKind.ConstructionDepth,
            _ => Utf8ResourceLimitKind.ConstructionWorkSteps }, error.ResourceKind);
        Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(128ul, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(checkpoint, owner.Budget.ReservedBytes); Assert.Equal(17, parent.Value);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void EveryWorkCutoffRestoresParentAndAcceptedOutputsRetainLoans(int route)
    {
        var input = Input(route); ulong work;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        { Run(route, input); work = measure.Budget.WorkSteps; }
        Assert.InRange(work, 1ul, 10000ul);
        for (var cutoff = 0ul; cutoff <= work; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
            var error = Record.Exception(() => Run(route, input));
            if (cutoff < work)
            {
                Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            }
            else { Assert.Null(error); Assert.Equal(route != 3, owner.Budget.ReservedBytes > 128); }
            Assert.Equal(0ul, owner.Budget.Depth); Assert.InRange(owner.Budget.WorkSteps, 2ul, cutoff + 2); Assert.Equal(17, parent.Value);
        }
    }
    [Fact]
    public void SearchFactStorageAdmitsCompleteStateAndKeepsPayloadsBorrowed()
    {
        var data = Data(); var bytes = Math.Max(128ul, Utf8ConstructionBudget.ObjectBytes<Utf8SearchFactData>());
        using var owner = new Utf8ConstructionScope(0, bytes, 2, ulong.MaxValue);
        var facts = Utf8SearchFacts.Create(Utf8SearchKind.ExactAsciiLiteral, data);
        Assert.Equal(bytes, owner.Budget.ReservedBytes); Assert.Equal(2ul, owner.Budget.WorkSteps);
        Assert.Same(data.LiteralUtf8, facts.LiteralUtf8); Assert.Same(data.RequiredWindowPrefilters, facts.RequiredWindowPrefilters);
        Assert.True(owner.Budget.ReservedBytes > 0);
        Assert.Equal(bytes, owner.Budget.ReservedBytes);
    }
    [Fact]
    public void SharedEmptyFactsAndDefaultFactsAcquireNoPerOwnerStorage()
    {
        using var owner = new Utf8ConstructionScope(0, 0, 0, 0);
        var facts = Utf8SearchFacts.Create(Utf8SearchKind.None);
        Assert.Empty(facts.LiteralUtf8); Assert.Empty(default(Utf8SearchFacts).AlternateLiteralsUtf8);
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LaterFailedFactStorageKeepsPreviouslyAcceptedFacts(bool work)
    {
        var data = Data(); var bytes = Math.Max(128ul, Utf8ConstructionBudget.ObjectBytes<Utf8SearchFactData>());
        using var owner = new Utf8ConstructionScope(0, work ? ulong.MaxValue : bytes, work ? 2ul : ulong.MaxValue, ulong.MaxValue);
        var facts = Utf8SearchFacts.Create(Utf8SearchKind.ExactAsciiLiteral, data); var checkpoint = owner.Budget.ReservedBytes;
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8SearchFacts.Create(Utf8SearchKind.ExactAsciiLiteral, data));
        Assert.Equal(work ? Utf8ResourceLimitKind.ConstructionWorkSteps : Utf8ResourceLimitKind.ConstructionBytes, error.ResourceKind);
        Assert.Equal(bytes, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes); Assert.Same(data.LiteralUtf8, facts.LiteralUtf8);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FailedBranchExpansionCannotMutateBorrowedTokensOrRetainEarlierOutputs(bool overflow)
    {
        var first = new AsciiSimplePatternToken[] { new((byte)'a'), new((byte)'b') };
        var branch = overflow
            ? Enumerable.Repeat(new AsciiSimplePatternToken(AsciiCharClass.FromBytes("ABCD"u8)), 4).ToArray()
            : new AsciiSimplePatternToken[] { new((byte)'a'), AsciiSimplePatternToken.Dot };
        var plan = Plan([first, branch], false); var original = first.ToArray();
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
        Assert.False(Utf8AsciiSimplePatternLowerer.TryExtractLiteralBranches(plan, out var result)); Assert.Empty(result);
        Assert.Equal(original, first); Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes); Assert.Equal(17, parent.Value);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EveryByteCutoffRecoversPartiallyAcquiredExpansion(bool folded)
    {
        var input = Plan([[new AsciiSimplePatternToken(AsciiCharClass.FromBytes("AaBb"u8)), new AsciiSimplePatternToken((byte)'!')]], folded);
        ulong peak;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        { measure.Budget.AllocateObject(17, static value => new ParentValue(value)); Assert.True(Utf8AsciiSimplePatternLowerer.TryExtractLiteralBranches(input, out _)); peak = measure.Budget.PeakBytes; }
        var success = false; var partialDenial = false;
        for (var cap = 128ul; cap <= peak; cap += 16)
        {
            using var owner = new Utf8ConstructionScope(0, cap, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
            var error = Record.Exception(() => Assert.True(Utf8AsciiSimplePatternLowerer.TryExtractLiteralBranches(input, out _)));
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
        var input = Plan([[new AsciiSimplePatternToken(AsciiCharClass.FromBytes("AaBb"u8)), new AsciiSimplePatternToken((byte)'!')]], folded);
        Assert.True(Utf8AsciiSimplePatternLowerer.TryExtractLiteralBranches(input, out var result));
        Assert.Equal(folded ? new[] { "a!", "b!" } : new[] { "A!", "B!", "a!", "b!" }, result.Select(Encoding.ASCII.GetString));
        var tooMany = Plan([Enumerable.Repeat(new AsciiSimplePatternToken(AsciiCharClass.FromBytes("ABCD"u8)), 4).ToArray()], false);
        Assert.False(Utf8AsciiSimplePatternLowerer.TryExtractLiteralBranches(tooMany, out _));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MultipleClassesKeepPrefixMajorOrderAndIndependentFinalPayloads(bool folded)
    {
        var suffix = new string('x', 256);
        var tokens = new[] { new AsciiSimplePatternToken(AsciiCharClass.FromBytes(folded ? "AaBb"u8 : "BA"u8)),
            new AsciiSimplePatternToken((byte)'!'), new AsciiSimplePatternToken(AsciiCharClass.FromBytes(folded ? "CcDd"u8 : "21"u8)) }
            .Concat(suffix.Select(value => new AsciiSimplePatternToken((byte)value))).ToArray();
        var original = tokens.ToArray();
        Assert.True(Utf8AsciiSimplePatternLowerer.TryExtractLiteralBranches(Plan([tokens], folded), out var result));
        Assert.Equal((folded ? new[] { "a!c", "a!d", "b!c", "b!d" } : new[] { "A!1", "A!2", "B!1", "B!2" })
            .Select(prefix => prefix + suffix), result.Select(Encoding.ASCII.GetString));
        result[0][^1] = (byte)'y';
        Assert.All(result.Skip(1), branch => Assert.Equal((byte)'x', branch[^1]));
        Assert.Equal(original, tokens);
    }
    [Fact]
    public void ExpansionAcceptsExactlySixtyFourBranchesAndAnEmptyLiteral()
    {
        var token = new AsciiSimplePatternToken(AsciiCharClass.FromBytes("DCBA"u8));
        var branch = Enumerable.Repeat(token, 3).ToArray();
        Assert.True(Utf8AsciiSimplePatternLowerer.TryExtractLiteralBranches(Plan([branch], false), out var result));
        Assert.Equal(64, result.Length);
        Assert.Equal(new[] { "AAA", "AAD", "ABA", "DDD" }, new[] { 0, 3, 4, 63 }.Select(index => Encoding.ASCII.GetString(result[index])));
        Assert.False(Utf8AsciiSimplePatternLowerer.TryExtractLiteralBranches(Plan([branch, []], false), out var declined));
        Assert.Empty(declined);
        Assert.True(Utf8AsciiSimplePatternLowerer.TryExtractLiteralBranches(Plan([[]], false), out var empty));
        Assert.Empty(Assert.Single(empty));
        foreach (var unsupported in new[] { AsciiSimplePatternToken.Dot, new AsciiSimplePatternToken(AsciiCharClass.Empty),
            new AsciiSimplePatternToken(new AsciiCharClass(1, 0, true)), new AsciiSimplePatternToken(AsciiCharClass.FromBytes("ABCDE"u8)) })
        {
            Assert.False(Utf8AsciiSimplePatternLowerer.TryExtractLiteralBranches(Plan([[unsupported]], false), out declined));
            Assert.Empty(declined);
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EveryExpansionCheckpointKeepsOriginalInterruptionAndParentCapacity(bool timeout)
    {
        var input = Plan([[new AsciiSimplePatternToken(AsciiCharClass.FromBytes("AB"u8)), new AsciiSimplePatternToken((byte)'!')]], false);
        var checkpoints = 0;
        using (var probe = Utf8ConstructionScope.Enter())
        {
            probe.Budget.CheckpointHook = _ => checkpoints++;
            Assert.True(Utf8AsciiSimplePatternLowerer.TryExtractLiteralBranches(input, out _));
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
            owner.Budget.CheckpointHook = _ => { if (++current == target) { if (timeout) ticks = 50; else token.Cancel(); } };
            var error = Record.Exception(() => Utf8AsciiSimplePatternLowerer.TryExtractLiteralBranches(input, out _));
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
        var regex = Utf8Regex.Compile((folded ? "[AaBb]" : "[AB]") + suffix,
            RegexOptions.CultureInvariant | (folded ? RegexOptions.IgnoreCase : RegexOptions.None), Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 1000000, MaxDepth = 100, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 },
            new() { MaxWorkSteps = 8000000, MaxDepth = 100, MaxBytes = 8 << 20, MaxInstructionCapacity = 1000000 },
            Timeout.InfiniteTimeSpan, default);
        Assert.Equal(folded ? NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals : NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        var input = Encoding.UTF8.GetBytes("😀A" + suffix + "中B" + suffix);
        Assert.Equal(2, regex.Count(input));
        Assert.Equal(4, regex.Match(input).IndexInBytes);
        Assert.Equal(513, regex.Match(input).LengthInBytes);
        Assert.Throws<ArgumentException>(() => regex.Count([.. input, 0xff]));
    }
    [Fact]
    public void AsciiMetadataRejectsNonAsciiBeforeAcquisition()
    {
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        Assert.Throws<ArgumentException>(() => owner.Budget.CopyAsciiText([0x80]));
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
        Assert.Equal(string.Empty, owner.Budget.CopyAsciiText([]));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2047)] [InlineData(2048)]
    [InlineData(2049)] [InlineData(4096)] [InlineData(4097)]
    public void AsciiMetadataBlocksKeepBulkWorkAndAdmittedText(int length)
    {
        var input = Enumerable.Range(0, length).Select(i => (byte)(i & 127)).ToArray();
        using var owner = Utf8ConstructionScope.Enter();
        owner.Budget.ReserveBytes(7);
        var steps = owner.Budget.WorkSteps;
        var result = owner.Budget.CopyAsciiText(input);
        Assert.Equal(Encoding.ASCII.GetString(input), result);
        Assert.Equal(7 + Utf8ConstructionBudget.StringBytes(length), owner.Budget.ReservedBytes);
        Assert.Equal(steps + (length == 0 ? 0UL : 2UL * (ulong)length + 1), owner.Budget.WorkSteps);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AsciiMetadataObservesOriginalInterruptionBeforeAnUnvisitedInvalidTail(bool timeout)
    {
        var input = Enumerable.Repeat((byte)'x', 8193).ToArray();
        input[^1] = 0x80;
        using var owner = Utf8ConstructionScope.Enter();
        owner.Budget.ReserveBytes(7);
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(50));
        owner.Budget.Deadline = deadline; owner.Budget.CancellationToken = token.Token;
        var points = 0;
        owner.Budget.CheckpointHook = _ => { if (++points == 2) { if (timeout) ticks = 50; else token.Cancel(); } };
        var error = Record.Exception(() => owner.Budget.CopyAsciiText(input));
        if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(error);
        else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
        Assert.Same(deadline, owner.Budget.Deadline);
        Assert.Equal(7UL, owner.Budget.ReservedBytes);
        Assert.Equal((ulong)input.Length, owner.Budget.WorkSteps);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AsciiMetadataEveryCheckpointReleasesOnlyItsAcquisition(bool timeout)
    {
        var input = Enumerable.Repeat((byte)'x', 4097).ToArray();
        var checkpoints = 0;
        using (var probe = Utf8ConstructionScope.Enter())
        {
            probe.Budget.CheckpointHook = _ => checkpoints++;
            Assert.Equal(new string('x', input.Length), probe.Budget.CopyAsciiText(input));
        }
        Assert.True(checkpoints >= 7, "Validation, conversion and publication need bounded interruption.");
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
            owner.Budget.CheckpointHook = _ => { if (++current == target) { if (timeout) ticks = 50; else token.Cancel(); } };
            string? published = null;
            var error = Record.Exception(() => published = owner.Budget.CopyAsciiText(input));
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(error);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
            Assert.Null(published);
            Assert.Same(deadline, owner.Budget.Deadline);
            Assert.Equal(7UL, owner.Budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false, 3000)] [InlineData(true, 3000)]
    [InlineData(false, 4097)] [InlineData(true, 4097)]
    public void WrappedLiteralKeepsItsAdmittedPublicRoute(bool compiled, int length)
    {
        var suffix = new string('x', length);
        var regex = Utf8Regex.Compile("(?:A" + suffix + ")",
            RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None), Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 1000000, MaxDepth = 100, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 },
            new() { MaxWorkSteps = 64000000, MaxDepth = 100, MaxBytes = 8 << 20, MaxInstructionCapacity = 1000000 },
            Timeout.InfiniteTimeSpan, default);
        // The speculative lowerer admits at most 4096 token cells. Larger
        // patterns keep scalar execution rather than expanding this optimizer.
        Assert.Equal(length == 3000 ? NativeExecutionKind.ExactAsciiLiteral : NativeExecutionKind.FallbackRegex,
            regex.Inspection.ExecutionKind);
        var input = Encoding.UTF8.GetBytes("😀A" + suffix + "中");
        Assert.Equal(1, regex.Count(input));
        Assert.Equal(4, regex.Match(input).IndexInBytes);
        Assert.Equal(suffix.Length + 1, regex.Match(input).LengthInBytes);
        Assert.Throws<ArgumentException>(() => regex.Count([.. input, 0xff]));
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
            var regex = new Utf8Regex(row.Pattern, RegexOptions.CultureInvariant | row.Flags | (compiled ? RegexOptions.Compiled : RegexOptions.None));
            var property = typeof(Utf8Regex).GetProperty("_verifierRuntime", BindingFlags.NonPublic | BindingFlags.Instance); Assert.NotNull(property);
            var runtime = Assert.IsType<Utf8VerifierRuntime>(property.GetValue(regex));
            Utf8OwnedVerifierAssertions.RetainsPreparedOwner(runtime);
            var input = Encoding.UTF8.GetBytes(row.Subject); Assert.True(regex.IsMatch(input)); Assert.Equal(2, regex.Count(input));
            var match = regex.Match(input); Assert.Equal(4, match.IndexInBytes); Assert.Equal(row.Pattern == @"\." ? 1 : 2, match.LengthInBytes);
            Assert.Equal(match.IndexInBytes, regex.MatchDetailed(input).IndexInBytes);
            var count = 0; foreach (var item in regex.EnumerateMatches(input)) { Assert.Equal(match.LengthInBytes, item.LengthInBytes); count++; }
            Assert.Equal(2, count); Assert.Equal(row.Replaced, regex.ReplaceToString(input, "X"));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null); Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        }
    }
}
