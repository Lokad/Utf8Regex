using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SplitStringGetterTests
{
    private const ulong Capacity = 65536;

    private static Utf8SliceProgram Program(Utf8Regex regex) =>
        (Utf8SliceProgram)typeof(Utf8Regex).GetField("_ownedSliceProgram", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex)!;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_boundaryMap")]
    private static extern ref Utf8BoundaryMap? BoundaryMap(ref Utf8ValueSplitEnumerator cursor);

    private static void CollectLoans()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    public static IEnumerable<object[]> WholeCases()
    {
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        foreach (var count in new[] { 0, 1 }) yield return [compiled, rtl, count];
    }

    [Theory]
    [MemberData(nameof(WholeCases))]
    public void EscapedWholeStringDoesNotRetainAnyPreparedSubjectLoan(bool compiled, bool rtl, int count)
    {
        const string text = "é😀 ab tail";
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0) | (rtl ? RegexOptions.RightToLeft : 0);
        var regex = new Utf8Regex("(zz)", options);
        var invocation = new Utf8SliceInvocation(new(0, 0, Capacity), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        var alias = VerifyEscapedValue(regex, Encoding.UTF8.GetBytes(text), text, count, ref invocation);
        CollectLoans();
        Assert.False(alias.IsAlive);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference VerifyEscapedValue(Utf8Regex regex, byte[] input, string text, int count,
        ref Utf8SliceInvocation invocation)
    {
        var alias = EscapeValue(regex, input, count, ref invocation, out var coordinates);
        CollectLoans();
        if (coordinates is not null) Assert.False(coordinates.IsAlive);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        Assert.True(invocation.Ledger.TryReserve(Capacity - 7));
        Assert.False(invocation.Ledger.TryReserve(1));
        invocation.Ledger.Release(Capacity - 7);
        Assert.Equal(text, alias);
        return new WeakReference(alias);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("é中")]
    [InlineData("")]
    public void AsciiBmpAndSharedEmptyStringsDoNotRetainPreparedSubjectLoans(string text)
    {
        var regex = new Utf8Regex("(zz)", RegexOptions.CultureInvariant);
        var invocation = new Utf8SliceInvocation(new(0, 0, Capacity), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        if (text.Length == 0)
        {
            Assert.Same(string.Empty, EscapeValue(regex, [], 1, ref invocation, out var coordinates));
            Assert.Null(coordinates);
            CollectLoans();
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
            return;
        }
        var alias = VerifyEscapedValue(regex, Encoding.UTF8.GetBytes(text), text, 1, ref invocation);
        CollectLoans();
        if (text.Length > 1) Assert.False(alias.IsAlive);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        // Encoding may cache a one-character string; its identity does not
        // carry a coordinate or subject loan.
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void IndependentSubstringDoesNotRetainThePreparedSubjectLoan(bool compiled, bool rtl)
    {
        const string text = "é😀 ab tail";
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0) | (rtl ? RegexOptions.RightToLeft : 0);
        var regex = new Utf8Regex("(ab)", options);
        var invocation = new Utf8SliceInvocation(new(0, 0, Capacity), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        var value = EscapeValue(regex, Encoding.UTF8.GetBytes(text), 0, ref invocation, out var coordinates);
        CollectLoans();
        Assert.NotNull(coordinates);
        Assert.False(coordinates.IsAlive);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(rtl ? " tail" : "é😀 ", value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string EscapeValue(Utf8Regex regex, byte[] input, int count, ref Utf8SliceInvocation invocation,
        out WeakReference? coordinates)
    {
        var cursor = Utf8OwnedSplitPreparation.Create(Program(regex), input, count, ref invocation);
        var map = BoundaryMap(ref cursor)!.Value;
        var offsets = (int[])typeof(Utf8BoundaryMap).GetField("_byteOffsets", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(map)!;
        coordinates = offsets.Length == 0 ? null : new WeakReference(offsets);
        Assert.True(cursor.MoveNext());
        var view = cursor.Current;
        var value = view.GetValueString();
        Assert.Equal(value, view.GetValueString());
        while (cursor.MoveNext()) { }
        return value;
    }
}
