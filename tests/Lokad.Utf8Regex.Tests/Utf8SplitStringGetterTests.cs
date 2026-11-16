using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SplitStringGetterTests
{
    public static IEnumerable<object[]> WholeCases()
    {
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        foreach (var count in new[] { 0, 1 }) yield return [compiled, rtl, count];
    }

    [Theory]
    [MemberData(nameof(WholeCases))]
    public void WholeStringNeedsNoPreparedSubjectStorage(bool compiled, bool rtl, int count)
    {
        const string text = "é😀 ab tail";
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0) | (rtl ? RegexOptions.RightToLeft : 0);
        Assert.Equal(text, ReadValue(new Utf8Regex("(zz)", options), Encoding.UTF8.GetBytes(text), count));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("é中")]
    [InlineData("")]
    public void AsciiBmpAndEmptyStringsDecodeOnlyTheRequestedBytes(string text)
    {
        var value = ReadValue(new Utf8Regex("(zz)", RegexOptions.CultureInvariant), Encoding.UTF8.GetBytes(text), 1);
        Assert.Equal(text, value);
        if (text.Length == 0) Assert.Same(string.Empty, value);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void SubstringRemainsValidAfterCursorCompletion(bool compiled, bool rtl)
    {
        const string text = "é😀 ab tail";
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0) | (rtl ? RegexOptions.RightToLeft : 0);
        Assert.Equal(rtl ? " tail" : "é😀 ", ReadValue(new Utf8Regex("(ab)", options), Encoding.UTF8.GetBytes(text), 0));
    }

    private static string ReadValue(Utf8Regex regex, byte[] input, int count)
    {
        var invocation = new Utf8SliceInvocation(new(0, 0, 65536), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        var cursor = Utf8OwnedSplitPreparation.Create(regex.Inspection.PreparedRegex.ScalarProgram!, input, count, ref invocation);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        Assert.True(cursor.MoveNext());
        var view = cursor.Current;
        var value = view.GetValueString();
        Assert.Equal(Encoding.UTF8.GetBytes(value), view.GetValueBytes().ToArray());
        while (cursor.MoveNext()) { }
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(value, view.GetValueString());
        return value;
    }
}
