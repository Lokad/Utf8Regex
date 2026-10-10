using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PatternByteSetQualificationTests
{
    private const RegexOptions CyrillicOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private sealed class ParentValue { internal readonly int Value = 17; }
    private static Utf8InvariantCyrillicLiteralCountStrategy Prepare(string pattern)
    {
        Assert.True(Utf8InvariantCyrillicLiteralCountStrategy.TryCreate(pattern, CyrillicOptions,
            Timeout.InfiniteTimeSpan, out var strategy));
        return strategy;
    }

    [Fact]
    public void PatternDependentCoreByteSetsHaveOwnedFieldState()
    {
        foreach (var type in new[] { typeof(Utf8EmittedKernelMatcher), typeof(Utf8InvariantCyrillicLiteralCountStrategy) })
        {
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.DoesNotContain(fields, field => field.FieldType.FullName?.StartsWith("System.Buffers.SearchValues") == true);
            Assert.Contains(fields, field => field.FieldType == typeof(PreparedByteSet));
        }
    }

    [Fact]
    public void CyrillicPreparationSharesByteWorkAndDepthAdmissionWithItsParent()
    {
        for (var denial = 0; denial < 3; denial++)
        {
            using var owner = new Utf8ConstructionScope(0, denial == 0 ? 112ul : ulong.MaxValue,
                denial == 1 ? 2ul : ulong.MaxValue, denial == 2 ? 0ul : ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static _ => new ParentValue());
            var checkpoint = owner.Budget.ObjectCheckpoint;
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Prepare("Она|он"));
            Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes,
                1 => Utf8ResourceLimitKind.ConstructionWorkSteps, _ => Utf8ResourceLimitKind.ConstructionDepth }, failure.ResourceKind);
            Assert.Equal(112ul, owner.Budget.ReservedBytes);
            Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(17, parent.Value);
        }
    }

    [Fact]
    public void CyrillicPreparationRejectsLateEndpointsAndRollsBackTheWholeChild()
    {
        const string pattern = " Она| она| ОН| он";
        ulong bytes, work, depth;
        using (var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            Assert.Equal(4, Prepare(pattern).Count(" Она она ОН он"u8));
            bytes = owner.Budget.PeakBytes; work = owner.Budget.WorkSteps; depth = owner.Budget.PeakDepth;
            Assert.True(bytes > 0 && work > 0 && depth > 0);
        }
        for (var denial = 0; denial < 3; denial++)
        {
            using var owner = new Utf8ConstructionScope(0, denial == 0 ? bytes - 1 : ulong.MaxValue,
                denial == 1 ? work - 1 : ulong.MaxValue, denial == 2 ? depth - 1 : ulong.MaxValue);
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Prepare(pattern));
            Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes,
                1 => Utf8ResourceLimitKind.ConstructionWorkSteps, _ => Utf8ResourceLimitKind.ConstructionDepth }, failure.ResourceKind);
            Assert.Equal(0ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Null(owner.Budget.ObjectCheckpoint); Assert.True(owner.Budget.WorkSteps > 0);
        }
    }

    [Fact]
    public void CyrillicDeclinesRetainSpentWorkAndReleaseSpeculativePayloads()
    {
        foreach (var pattern in new[] { "Она|.", "Она|", "Она|abc", "Она|😀", "123|456" })
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
            Assert.False(Utf8InvariantCyrillicLiteralCountStrategy.TryCreate(pattern, CyrillicOptions,
                Timeout.InfiniteTimeSpan, out var strategy));
            Assert.Null(strategy); Assert.True(owner.Budget.WorkSteps > 0);
            Assert.Equal(0ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Null(owner.Budget.ObjectCheckpoint);
        }
    }

    [Fact]
    public void CyrillicSharedAndUnequalAnchorsKeepScalarResultsAndStrictValidation()
    {
        foreach (var pattern in new[] { "Она", "Она|он", " Она| он", "Она| он|он!", "Я|я|Ты|ты|Он|он|Мы|мы" })
        {
            const string text = "😀Она он она ОН! он! Ты мы Я é中";
            var input = Encoding.UTF8.GetBytes(text);
            var expected = new Regex(pattern, CyrillicOptions).Count(text);
            var strategy = Prepare(pattern);
            Assert.Equal(expected, strategy.Count(input));
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var prefilter = (PreparedMultiLiteralPackedNibbleSimdPrefilter)typeof(Utf8InvariantCyrillicLiteralCountStrategy)
                .GetField("_correlatedPrefilter", flags)!.GetValue(strategy)!;
            var anchor = typeof(Utf8InvariantCyrillicLiteralCountStrategy).GetField("_sharedAnchorSearch", flags)?.GetValue(strategy);
            var masks = typeof(Utf8InvariantCyrillicLiteralCountStrategy).GetField("_sharedAnchorLiteralMasks", flags)!.GetValue(strategy);
            if (prefilter.HasValue || !pattern.Contains('|')) { Assert.Null(anchor); Assert.Null(masks); }
            else if (pattern is "Она|он" or " Она| он" or "Я|я|Ты|ты|Он|он|Мы|мы")
            { Assert.IsType<PreparedByteSet>(anchor); Assert.IsType<byte[]>(masks); }
            foreach (var compiled in new[] { RegexOptions.None, RegexOptions.Compiled })
            {
                var regex = new Utf8Regex(pattern, CyrillicOptions | compiled);
                Assert.Equal(expected, regex.Count(input));
                Assert.Throws<ArgumentException>(() => regex.Count(input.Concat(new byte[] { 0xff }).ToArray()));
                using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
                Assert.Throws<OperationCanceledException>(() => regex.Count(input, cancellation.Token));
            }
        }
    }

    [Fact]
    public void EmittedAnchorAdmissionRejectsBeforePublicationAndKeepsFindCountResults()
    {
        var regex = new Utf8Regex(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", RegexOptions.CultureInvariant);
        var prepared = regex.Inspection.PreparedRegex;
        using (var owner = new Utf8ConstructionScope(0, 48, ulong.MaxValue, ulong.MaxValue))
        {
            var parent = owner.Budget.AllocateArray<byte>(1, false); parent[0] = 0xa5;
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                Utf8EmittedKernelMatcher.TryCreate(prepared, out _));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
            Assert.Equal(48ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Null(owner.Budget.ObjectCheckpoint); Assert.Equal(0xa5, parent[0]);
        }
        Assert.True(Utf8EmittedKernelMatcher.TryCreate(prepared, out var matcher));
        Assert.Equal(2, matcher.Count("!record Customer struct Worker class lower"u8));
        Assert.Equal(1, matcher.FindNext("!record Customer struct Worker"u8, 0, out var length));
        Assert.Equal(15, length);
        Assert.True(Utf8EmittedKernelMatcher.TryCreate(prepared.StructuralIdentifierFamilyPlan,
            prepared.SearchPlan, out var direct));
        Assert.Equal(2, direct.Count("!record Customer struct Worker class lower"u8));
    }
}
