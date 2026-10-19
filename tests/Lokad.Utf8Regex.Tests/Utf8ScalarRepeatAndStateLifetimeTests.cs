using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarRepeatAndStateLifetimeTests
{
    public static IEnumerable<object[]> PublicCases()
    {
        foreach (var pattern in new[] { @"^ab(?:c[ab]){1,3}x$", @"\w+\s+Holmes" })
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true }) yield return [pattern, RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None) | (rtl ? RegexOptions.RightToLeft : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(PublicCases))]
    public void FullPublicOperationsKeepOrdinaryCompiledRtlAndUnicodeFallbackSemantics(string pattern, RegexOptions options)
    {
        Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options,
            ["", "abx", "abcacbx", "abcacbcax", "abcacbcacax", "abcacbx\n", "John Holmes", "é Holmes", "中 Holmes",
             "John Holmes é Holmes", "\U0001f600é Holmes\U0001f600"], options);
    }

    private readonly record struct ReferenceState(object? Graph, int Value);



    public static IEnumerable<object[]> NativeEndAnchorCases()
    {
        foreach (var pattern in new[] { @"^ab(?:c[ab]){1,3}x$", @"\Aab(?:c[ab]){1,3}x\Z", @"\Aab(?:c[ab]){1,3}x\z",
            @"(?m)^ab(?:c[ab]){1,3}x$", @"(?m)\Aab(?:c[ab]){1,3}x\Z", @"(?m)\Aab(?:c[ab]){1,3}x\z" })
        foreach (var compiled in new[] { false, true }) yield return [pattern,
            RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(NativeEndAnchorCases))]
    public void NativeExpandedBranchesDistinguishAbsoluteFinalNewlineAndLineEnds(string pattern, RegexOptions options)
    {
        var oracle = new Regex(pattern, options);
        var regex = new Utf8Regex(pattern, options);
        foreach (var text in new[] { "abcacbx", "abcacbx\n", "abcacbx\n\n", "abcacbx\nrest", "\nabcacbx\n", "\r\nabcacbx\n" })
        {
            var input = Encoding.ASCII.GetBytes(text);
            Assert.Equal(oracle.Replace(text, "Q"), regex.ReplaceToString(input, "Q"));
            Assert.Equal(Encoding.ASCII.GetBytes(oracle.Replace(text, "Q")), regex.Replace(input, "Q"));
            var expected = oracle.EnumerateMatches(text);
            var actual = regex.EnumerateMatches(input);
            while (expected.MoveNext())
            {
                Assert.True(actual.MoveNext());
                Assert.Equal(expected.Current.Index, actual.Current.IndexInUtf16);
                Assert.Equal(expected.Current.Length, actual.Current.LengthInUtf16);
            }
            Assert.False(actual.MoveNext());
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
    }

    [Fact]
    public void ReferenceLoansHaveExactAdmissionBoundedRetentionAndClearGraphSlots()
    {
        var denied = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, 1), Regex.InfiniteMatchTimeout, CancellationToken.None);
        var buffer = new Utf8InvocationStateBuffer<ReferenceState>();
        var value = new ReferenceState(new object(), 1);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var breach = buffer.TryPush(value, ref denied);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, breach);
        Assert.Equal(0, allocated);
        var retained = Utf8ExactStatePool<ReferenceState>.RetainedBytes;
        var finite = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, 4096), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.None, buffer.TryPush(value, ref finite));
        Assert.Equal(Utf8ConstructionBudget.ArrayBytes<ReferenceState>(16), finite.Ledger.ReservedBytes);
        buffer.Pop();
        buffer.Release(ref finite);
        Assert.Equal(retained, Utf8ExactStatePool<ReferenceState>.RetainedBytes);
        foreach (var capacity in new[] { 16, 32, 64, 128, 256 })
        {
            var items = Utf8ExactStatePool<ReferenceState>.Rent(capacity, true);
            Array.Fill(items, value);
            Utf8ExactStatePool<ReferenceState>.Return(items, true);
            items = Utf8ExactStatePool<ReferenceState>.Rent(capacity, false);
            Assert.All(items, static item => Assert.Equal(default, item));
            Utf8ExactStatePool<ReferenceState>.Return(items, true);
            Assert.InRange(Utf8ExactStatePool<ReferenceState>.RetainedBytes, 0, Utf8ExactStatePool<ReferenceState>.MaximumRetainedBytes);
        }
    }
}
