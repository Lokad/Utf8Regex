using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SubstringPreparationQualificationTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static object? Field(PreparedSubstringSearch search, string name) =>
        typeof(PreparedSubstringSearch).GetField(name, Fields)!.GetValue(search);
    private delegate int ReverseCount(ReadOnlySpan<byte> input, in AsciiStructuralIdentifierFamilyPlan plan, byte[] suffix, int minimum);
    private delegate bool TrailingMatch(ReadOnlySpan<byte> input, AsciiOrderedLiteralWindowPlan plan,
        PreparedAsciiFindPlan find, int minimum, out int index);

    [Fact]
    public void CanonicalFoldedBytesAreBorrowedAndOnlyUsedShiftTablesAreRetained()
    {
        foreach (var length in new[] { 0, 1, 2, 3, 8, 9, 10, 11, 16, 33 })
        foreach (var uppercase in new[] { false, true })
        {
            var needle = Encoding.ASCII.GetBytes(new string(uppercase ? 'A' : 'a', length));
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
            var search = new PreparedSubstringSearch(needle, true);
            Assert.Same(needle, search.Needle);
            var folded = (byte[]?)Field(search, "_foldedNeedle");
            var shifts = (int[]?)Field(search, "_ignoreCaseShiftTable");
            if (length <= 1) Assert.Null(folded);
            else
            {
                Assert.Equal(Enumerable.Repeat((byte)'a', length), folded!);
                if (uppercase) Assert.NotSame(needle, folded);
                else Assert.Same(needle, folded);
            }
            if (length <= 8) Assert.Null(shifts);
            else Assert.Equal(256, shifts!.Length);
            var expected = uppercase && length >= 2 ? Utf8ConstructionBudget.ArrayBytes<byte>(length) : 0;
            if (length >= 9) expected += Utf8ConstructionBudget.ArrayBytes<int>(256);
            Assert.Equal(expected, owner.Budget.ReservedBytes);
            Assert.Equal(expected, owner.Budget.PeakBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Null(owner.Budget.ObjectCheckpoint);
        }
    }

    [Fact]
    public void ExactAnchorScansAreAdmittedAndConstantBorrowedSearchesNeedNoOwnerCapacity()
    {
        foreach (var length in new[] { 0, 1, 2, 3, 4, 8, 16, 17, 33 })
        {
            var needle = Encoding.ASCII.GetBytes(new string('a', length));
            var scans = length is >= 4 and <= 16;
            using var owner = new Utf8ConstructionScope(0, 0, scans ? (ulong)(3 * length) : 0, scans ? 1ul : 0);
            var search = new PreparedSubstringSearch(needle, false);
            Assert.Same(needle, search.Needle);
            Assert.Equal(scans ? (ulong)(3 * length) : 0, owner.Budget.WorkSteps);
            Assert.Equal(0ul, owner.Budget.ReservedBytes);
            owner.Dispose();
            if (!scans) continue;
            using var denied = new Utf8ConstructionScope(0, 0, (ulong)(3 * length - 1), 1);
            Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => new PreparedSubstringSearch(needle, false)).ResourceKind);
            Assert.Equal(0ul, denied.Budget.Depth);
            Assert.Equal(0ul, denied.Budget.ReservedBytes);
        }
        foreach (var needle in new[] { Array.Empty<byte>(), "A"u8.ToArray() })
        {
            using var zero = new Utf8ConstructionScope(0, 0, 0, 0);
            var search = new PreparedSubstringSearch(needle, true);
            Assert.Equal(needle.Length == 0 ? 0 : 1, search.IndexOf("xA"u8));
            Assert.Equal(0ul, zero.Budget.WorkSteps);
        }
    }

    [Fact]
    public void EverySubstringByteAndWorkCutoffRestoresItsParentAndTransfersOnlyFinalArrays()
    {
        var needle = "ABCDEFGHIJKL"u8.ToArray();
        ulong peak, work;
        using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            _ = new PreparedSubstringSearch(needle, true);
            peak = funded.Budget.PeakBytes; work = funded.Budget.WorkSteps;
        }
        Assert.Equal(Utf8ConstructionBudget.ArrayBytes<byte>(12) + Utf8ConstructionBudget.ArrayBytes<int>(256), peak);
        Assert.InRange(work, 1ul, 3000ul);
        foreach (var bytes in new[] { true, false })
        {
            var successes = 0;
            for (var cutoff = 0ul; cutoff <= (bytes ? peak : work); cutoff++)
            {
                using var owner = new Utf8ConstructionScope(0, bytes ? 112 + cutoff : ulong.MaxValue,
                    bytes ? ulong.MaxValue : 2 + cutoff, ulong.MaxValue);
                var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
                var checkpoint = owner.Budget.ObjectCheckpoint;
                var failure = Record.Exception(() => new PreparedSubstringSearch(needle, true));
                if (failure is null) { successes++; Assert.Equal(112 + peak, owner.Budget.ReservedBytes); }
                else
                {
                    Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps,
                        Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                    Assert.Equal(112ul, owner.Budget.ReservedBytes);
                }
                Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
                Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth);
                Assert.Equal(0ul, owner.Budget.Reserved);
                Assert.True(bytes ? owner.Budget.PeakBytes <= 112 + cutoff : owner.Budget.WorkSteps <= 2 + cutoff);
            }
            Assert.Equal(1, successes);
        }
    }

    [Fact]
    public void DepthAndLateShiftDenialsRecoverInTheSameOwnerWithoutRefundingWork()
    {
        var needle = "ABCDEFGHIJKL"u8.ToArray();
        using var owner = new Utf8ConstructionScope(0, 112 + Utf8ConstructionBudget.ArrayBytes<byte>(needle.Length), 3000, 1);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        owner.Budget.EnterDepth();
        try
        {
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => new PreparedSubstringSearch(needle, true)).ResourceKind);
            Assert.Equal(2ul, owner.Budget.WorkSteps);
        }
        finally { owner.Budget.LeaveDepth(); }
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => new PreparedSubstringSearch(needle, true)).ResourceKind);
        var spent = owner.Budget.WorkSteps;
        Assert.True(spent > 2);
        Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        var recovered = new PreparedSubstringSearch("xy"u8.ToArray(), true);
        Assert.Equal(1, recovered.LastIndexOf("!XY"u8)); Assert.True(owner.Budget.WorkSteps > spent);
        Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(17, parent.Value);
    }

    [Fact]
    public void PreparedTiersAgreeWithIndependentByteCandidateOracles()
    {
        foreach (var length in new[] { 0, 1, 2, 3, 8, 9, 10, 11, 12, 16, 17, 32, 33, 64 })
        foreach (var binary in new[] { false, true })
        foreach (var ignore in new[] { false, true })
        {
            // Folded candidate kernels receive admitted ASCII needles. Exact
            // byte search can also compare binary needles; both see binary subjects.
            var needle = Enumerable.Range(0, length).Select(i => binary && !ignore && i % 5 == 0
                ? (byte)0x80 : (byte)("Az!9B_cD"[i % 8])).ToArray();
            var flipped = needle.Select(b => b is >= (byte)'A' and <= (byte)'Z' ? (byte)(b + 32)
                : b is >= (byte)'a' and <= (byte)'z' ? (byte)(b - 32) : b).ToArray();
            var search = new PreparedSubstringSearch(needle, ignore);
            foreach (var input in new[] { Array.Empty<byte>(), needle, flipped,
                Enumerable.Repeat(binary ? (byte)0x80 : (byte)'?', 97).Concat(needle).Concat("??"u8.ToArray()).Concat(flipped).ToArray(),
                Enumerable.Repeat(binary ? (byte)0x80 : (byte)'?', 257).ToArray() })
            {
                bool At(int start) => start + needle.Length <= input.Length &&
                    Enumerable.Range(0, needle.Length).All(i => ignore ? Fold(input[start + i]) == Fold(needle[i]) : input[start + i] == needle[i]);
                var hits = Enumerable.Range(0, Math.Max(0, input.Length - needle.Length + 1)).Where(At).ToArray();
                var expectedFirst = hits.Length == 0 ? -1 : hits[0];
                var actualFirst = search.IndexOf(input);
                Assert.True(expectedFirst == actualFirst,
                    $"First: length={length}, binary={binary}, ignore={ignore}, input={Convert.ToHexString(input)}, expected={expectedFirst}, actual={actualFirst}");
                var expectedLast = hits.Length == 0 ? -1 : hits[^1];
                var actualLast = search.LastIndexOf(input);
                Assert.True(expectedLast == actualLast,
                    $"Last: length={length}, binary={binary}, ignore={ignore}, input={Convert.ToHexString(input)}, expected={expectedLast}, actual={actualLast}");
                if (length == 0) continue;
                var count = 0; var next = 0;
                foreach (var hit in hits) if (hit >= next) { count++; next = hit + length; }
                Assert.Equal(count, search.CountWithMetrics(input, out _, out _));
                if (ignore) foreach (var tier in Enum.GetValues<PreparedIgnoreCaseSearchTier>().Where(t => t != PreparedIgnoreCaseSearchTier.None))
                {
                    var actualCount = search.CountIgnoreCaseWithTier(input, tier, out _, out _);
                    Assert.True(count == actualCount,
                        $"Count: length={length}, binary={binary}, tier={tier}, input={Convert.ToHexString(input)}, expected={count}, actual={actualCount}");
                }
            }
        }
        static byte Fold(byte b) => b is >= (byte)'A' and <= (byte)'Z' ? (byte)(b + 32) : b;
    }

    [Fact]
    public void SuffixPartsRetainOneCanonicalSearchAndDeadPreparationOverloadsAreAbsent()
    {
        Assert.Equal(7, typeof(PreparedSubstringSearch).GetFields(Fields).Length);
        Assert.Equal(4, typeof(AsciiStructuralSuffixPart).GetFields(Fields).Length);
        var literal = "suffix"u8.ToArray();
        var part = AsciiStructuralSuffixPart.CreateLiteral(literal);
        var property = typeof(AsciiStructuralSuffixPart).GetProperty("LiteralSearch", Fields);
        Assert.NotNull(property);
        var search = Assert.IsType<PreparedSubstringSearch>(property.GetValue(part));
        Assert.Same(literal, search.Needle); Assert.Same(literal, part.LiteralUtf8);
        Assert.Empty(typeof(AsciiStructuralSuffixPart).GetFields(Fields).Where(f => f.FieldType == typeof(byte[])));
        var methods = typeof(PreparedSubstringSearch).GetMethods(BindingFlags.NonPublic | BindingFlags.Static);
        Assert.DoesNotContain(methods, m => m.Name is "CreateFoldedNeedle" or "CreateIgnoreCaseShiftTable");
        Assert.DoesNotContain(methods, m => m.Name == "TryIndexOfIgnoreCaseFirstLastVector" && m.GetParameters().Length is 3 or 5);
    }

    [Fact]
    public void RuntimeWindowAndReverseSuffixKernelsBorrowPreviouslyPreparedSearches()
    {
        var literal = "tail"u8.ToArray();
        var family = new AsciiStructuralIdentifierFamilyPlan(["head"u8.ToArray()], AsciiCharClass.ForByte((byte)' '), 1,
            null, null, 0, 0, literal, null, Utf8BoundaryRequirement.Boundary, Utf8BoundaryRequirement.Boundary);
        var reverse = typeof(Utf8AsciiStructuralIdentifierFamilyExecutor).GetMethod("CountSuffixOnlyReverseLiteral",
            BindingFlags.NonPublic | BindingFlags.Static)!.CreateDelegate<ReverseCount>();
        var window = new AsciiOrderedLiteralWindowPlan("head"u8.ToArray(), null, literal, null, 8, 0, true,
            default, null, false, Utf8BoundaryRequirement.None, Utf8BoundaryRequirement.None,
            Utf8BoundaryRequirement.Boundary, Utf8BoundaryRequirement.Boundary);
        var trailing = typeof(AsciiOrderedLiteralWindowExecutor).GetMethod("TryFindNextTrailingBoundaryMatch",
            BindingFlags.NonPublic | BindingFlags.Static)!.CreateDelegate<TrailingMatch>();
        using var zero = new Utf8ConstructionScope(0, 0, 0, 0);
        Assert.Equal(2, reverse("head tail; head tail"u8, family, literal, 1));
        Assert.True(trailing("xtail tail!"u8, window, default, 0, out var index)); Assert.Equal(6, index);
        Assert.Equal(0ul, zero.Budget.WorkSteps); Assert.Equal(0ul, zero.Budget.PeakBytes); Assert.Equal(0ul, zero.Budget.Depth);
    }

    [Fact]
    public void FindAndSearchPlanConstructionCallersShareTheSubstringScanAdmission()
    {
        var literal = "anchor"u8.ToArray();
        foreach (var build in new Action[] { () => PreparedAsciiFindPlan.CreateLiteral(literal),
            () => PreparedAsciiFindPlan.CreateFixedDistanceLiteral(literal, 2),
            () => Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(Utf8SearchKind.ExactAsciiLiteral,
                new Utf8SearchFactData { LiteralUtf8 = literal })) })
        {
            using var denied = new Utf8ConstructionScope(0, ulong.MaxValue, 0, ulong.MaxValue);
            Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps,
                Assert.Throws<Utf8ResourceLimitExceededException>(build).ResourceKind);
            Assert.Equal(0ul, denied.Budget.Depth); Assert.Equal(0ul, denied.Budget.ReservedBytes);
        }
    }

    [Fact]
    public void PublicLiteralSearchKeepsScalarRangesStrictValidationAndCancellation()
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft,
            RegexOptions.RightToLeft | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex("(abcdefghijkl)", options | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var input = "😀aBcDeFgHiJkL中ABcDEFGHIJKLé"u8.ToArray();
            Assert.Equal(2, regex.Count(input));
            var match = regex.MatchDetailed(input);
            Assert.Equal(options.HasFlag(RegexOptions.RightToLeft) ? 19 : 4, match.IndexInBytes);
            Assert.Equal(12, match.LengthInBytes); Assert.Equal(12, match.LengthInUtf16);
            var unicode = new Utf8Regex("(éabcdefghijkl)", options | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var scalarMatch = unicode.MatchDetailed("😀ÉABCDEFGHIJKL中"u8);
            Assert.Equal((4, 14, 13), (scalarMatch.IndexInBytes, scalarMatch.LengthInBytes, scalarMatch.LengthInUtf16));
            Assert.Equal(3, new Utf8Regex(".", options | RegexOptions.CultureInvariant).Count("😀中é"u8));
            var malformed = input.Concat(new byte[] { 0xff }).ToArray();
            Assert.Throws<ArgumentException>(() => regex.IsMatch(malformed));
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => regex.Count(input, cancelled.Token));
        }
    }
}
