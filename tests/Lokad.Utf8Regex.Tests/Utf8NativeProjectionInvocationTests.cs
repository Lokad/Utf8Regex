using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NativeProjectionInvocationTests
{
    private static Utf8SliceInvocation Metered()
        => new(new Utf8ExecutionLimits(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);

    public static IEnumerable<object[]> BoundaryCases()
    {
        foreach (var offset in new[] { 2045, 2046, 2047, 2048, 2049 })
        foreach (var scalar in new[] { "a", "é", "中", "\U0001f600" }) yield return [offset, scalar];
    }
    [Theory]
    [MemberData(nameof(BoundaryCases))]
    public void MeteredValidationKeepsScalarsAcrossBlockBoundaries(int offset, string scalar)
    {
        var text = new string('x', offset) + scalar + string.Concat(Enumerable.Repeat("é中\U0001f600", 1000));
        var input = Encoding.UTF8.GetBytes(text);
        var invocation = Metered();
        var expected = Utf8Validation.Validate(input);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8Validation.TryValidate(input, ref invocation, out var actual));
        Assert.Equal(expected.ByteLength, actual.ByteLength);
        Assert.Equal(text.Length, actual.Utf16Length);
        Assert.Equal(expected.IsAscii, actual.IsAscii);
        Assert.Equal(expected.ContainsSupplementaryScalars, actual.ContainsSupplementaryScalars);
        Assert.True(invocation.Budget.Steps >= (ulong)input.Length / 2048);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        var only = Metered();
        Utf8Validation.ThrowIfInvalidOnly(input, ref only);
        Assert.Equal(invocation.Budget.Steps, only.Budget.Steps);
        Assert.Equal(0UL, only.Ledger.ReservedBytes);
    }
    public static IEnumerable<object[]> InvalidCases()
    {
        byte[][] invalid = [[0x80], [0xc0, 0x80], [0xe0, 0x9f, 0x80], [0xed, 0xa0, 0x80],
            [0xf4, 0x90, 0x80, 0x80], [0xf5, 0x80, 0x80, 0x80], [0xe2, 0x80],
            [0xc1, 0xbf], [0xf0, 0x8f, 0xbf, 0xbf], [0xf0, 0x9f, 0x98], [0x80, 0x80, 0x80, 0x80]];
        foreach (var offset in new[] { 0, 2045, 2046, 2047, 2048, 2049, 4095 })
        foreach (var bytes in invalid) yield return [offset, bytes];
    }
    [Theory]
    [MemberData(nameof(InvalidCases))]
    public void MeteredValidationPreservesFirstInvalidByteAndFailureMessage(int offset, byte[] invalid)
    {
        byte[] input = [.. Enumerable.Repeat((byte)'x', offset), .. invalid, .. "xyz"u8.ToArray()];
        var expected = Assert.Throws<ArgumentException>(() => Utf8Validation.Validate(input));
        var invocation = Metered();
        var actual = Assert.Throws<ArgumentException>(() => Utf8Validation.TryValidate(input, ref invocation, out _));
        Assert.Equal(expected.ParamName, actual.ParamName);
        Assert.Equal(expected.Message, actual.Message);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        var only = Metered();
        var invalidOnly = Assert.Throws<ArgumentException>(() => Utf8Validation.ThrowIfInvalidOnly(input, ref only));
        Assert.Equal(expected.ParamName, invalidOnly.ParamName);
        Assert.Equal(expected.Message, invalidOnly.Message);
        Assert.Equal(0UL, only.Ledger.ReservedBytes);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void NativeCursorProjectionRetainsItsAlreadyExpiredDeadline(int family)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        var pattern = family switch { 2 or 8 => "中", 3 or 4 => "target|other", 5 => "ta[rg]get", _ => "target" };
        var options = RegexOptions.CultureInvariant | (family is 1 or 4 or 7 ? RegexOptions.IgnoreCase : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options);
        var plan = Assert.IsType<Utf8PreparedRegex>(typeof(Utf8Regex).GetProperty("PreparedRegex", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(regex));
        var text = "é中\U0001f600 target";
        var input = Encoding.UTF8.GetBytes(text);
        var literal = Encoding.UTF8.GetBytes(pattern);
        var budget = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(200));
        var cursor = family switch
        {
            6 or 7 => new Utf8OperationMatchCursor(input, literal, budget, family == 6 ? Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralSuffix : Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffix),
            8 => new Utf8OperationMatchCursor(input, plan, literal, NativeExecutionKind.ExactUtf8Literal, budget, false, true),
            _ => Utf8CompiledOperationCursorFactory.CreateMatchCursor(plan, input, Utf8Validation.Validate(input), budget),
        };
        clock.Advance(250);
        var expired = false;
        try { _ = cursor.MoveNext(); }
        catch (Utf8ExecutionDeadlineExpiredException) { expired = true; }
        Assert.True(expired);
        Assert.False(cursor.CurrentValueMatch.Success);
    }
    public static IEnumerable<object[]> PublicCases()
    {
        foreach (var pattern in new[] { "target", "中", "target|other", "ta[rg]get", "target$" })
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true }) yield return [pattern, RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None) | (rtl ? RegexOptions.RightToLeft : RegexOptions.None)];
    }
    [Theory]
    [MemberData(nameof(PublicCases))]
    public void PublicProjectionPreservesAllOwnedOperations(string pattern, RegexOptions options)
        => Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options,
            ["", "target", "other", "中target", "é中\U0001f600 target other 中", "é中\U0001f600 target\n", "é中\U0001f600 miss"], options);
}
