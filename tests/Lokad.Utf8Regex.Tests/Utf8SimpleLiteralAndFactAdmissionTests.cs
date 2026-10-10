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
        using var owner = new Utf8ConstructionScope(0, denial == 0 ? 112ul : ulong.MaxValue,
            denial == 2 ? 2ul : ulong.MaxValue, denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, input));
        Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes, 1 => Utf8ResourceLimitKind.ConstructionDepth,
            _ => Utf8ResourceLimitKind.ConstructionWorkSteps }, error.ResourceKind);
        Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Equal(112ul, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint); Assert.Equal(17, parent.Value);
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
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
            var error = Record.Exception(() => Run(route, input));
            if (cutoff < work)
            {
                Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            }
            else { Assert.Null(error); Assert.Equal(route != 3, owner.Budget.ReservedBytes > 112); }
            Assert.Equal(0ul, owner.Budget.Depth); Assert.InRange(owner.Budget.WorkSteps, 2ul, cutoff + 2); Assert.Equal(17, parent.Value);
        }
    }
    [Fact]
    public void SearchFactStorageAdmitsCompleteStateAndKeepsPayloadsBorrowed()
    {
        var data = Data(); var bytes = Utf8ConstructionBudget.ObjectBytes<Utf8SearchFactData>()
            + Utf8ConstructionBudget.ObjectBytes<(object, Utf8ConstructionBudget.ObjectAllocation?, ulong)>();
        using var owner = new Utf8ConstructionScope(0, bytes, 2, ulong.MaxValue);
        var facts = Utf8SearchFacts.Create(Utf8SearchKind.ExactAsciiLiteral, data);
        Assert.Equal(bytes, owner.Budget.ReservedBytes); Assert.Equal(2ul, owner.Budget.WorkSteps);
        Assert.Same(data.LiteralUtf8, facts.LiteralUtf8); Assert.Same(data.RequiredWindowPrefilters, facts.RequiredWindowPrefilters);
        Assert.NotNull(owner.Budget.ObjectCheckpoint); Assert.Null(owner.Budget.ObjectCheckpoint.Previous);
        Assert.Equal(bytes, owner.Budget.ObjectCheckpoint.Bytes);
    }
    [Fact]
    public void SharedEmptyFactsAndDefaultFactsAcquireNoPerOwnerStorage()
    {
        using var owner = new Utf8ConstructionScope(0, 0, 0, 0);
        var facts = Utf8SearchFacts.Create(Utf8SearchKind.None);
        Assert.Empty(facts.LiteralUtf8); Assert.Empty(default(Utf8SearchFacts).AlternateLiteralsUtf8);
        Assert.Equal(0ul, owner.Budget.ReservedBytes); Assert.Null(owner.Budget.ObjectCheckpoint);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LaterFailedFactStorageKeepsPreviouslyAcceptedFacts(bool work)
    {
        var data = Data(); var bytes = Utf8ConstructionBudget.ObjectBytes<Utf8SearchFactData>() + 64;
        using var owner = new Utf8ConstructionScope(0, work ? ulong.MaxValue : bytes, work ? 2ul : ulong.MaxValue, ulong.MaxValue);
        var facts = Utf8SearchFacts.Create(Utf8SearchKind.ExactAsciiLiteral, data); var checkpoint = owner.Budget.ObjectCheckpoint;
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8SearchFacts.Create(Utf8SearchKind.ExactAsciiLiteral, data));
        Assert.Equal(work ? Utf8ResourceLimitKind.ConstructionWorkSteps : Utf8ResourceLimitKind.ConstructionBytes, error.ResourceKind);
        Assert.Equal(bytes, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint); Assert.Same(data.LiteralUtf8, facts.LiteralUtf8);
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
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
        Assert.False(Utf8AsciiSimplePatternLowerer.TryExtractLiteralBranches(plan, out var result)); Assert.Empty(result);
        Assert.Equal(original, first); Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint); Assert.Equal(17, parent.Value);
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
        for (var cap = 112ul; cap <= peak; cap += 16)
        {
            using var owner = new Utf8ConstructionScope(0, cap, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
            var error = Record.Exception(() => Assert.True(Utf8AsciiSimplePatternLowerer.TryExtractLiteralBranches(input, out _)));
            if (error is null) { success = true; Assert.True(owner.Budget.ReservedBytes > 112); }
            else
            {
                Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                partialDenial |= owner.Budget.PeakBytes > 112; Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            }
            Assert.InRange(owner.Budget.PeakBytes, 112ul, cap); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(17, parent.Value);
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
    [Fact]
    public void AsciiMetadataRejectsNonAsciiBeforeAcquisition()
    {
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        Assert.Throws<ArgumentException>(() => owner.Budget.CopyAsciiText([0x80]));
        Assert.Equal(0ul, owner.Budget.ReservedBytes); Assert.Null(owner.Budget.ObjectCheckpoint);
        Assert.Equal(string.Empty, owner.Budget.CopyAsciiText([]));
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
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null); Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        }
    }
}
