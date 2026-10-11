using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ConstructionTextTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(1024)]
    public void CreatedTextAdmitsItsTerminatorAndLengthBasedCapacity(int length)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var source = new string('é', length);
        var value = owner.Budget.CopyText(source);
        Assert.Equal(source, value);
        Assert.Equal(length == 0 ? 0ul : Utf8ConstructionBudget.StringBytes(length), owner.Budget.ReservedBytes);
        if (length == 0) Assert.Same(string.Empty, value);
        else Assert.NotSame(source, value);
    }

    [Fact]
    public void StringCapacityCoversActualWarmedAllocation()
    {
        using var owner = Utf8ConstructionScope.Enter();
        var source = new string('a', 129);
        _ = owner.Budget.CopyText(source);
        var held = owner.Budget.ReservedBytes;
        var before = GC.GetAllocatedBytesForCurrentThread();
        var value = owner.Budget.CopyText(source);
        var acquired = (ulong)(GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(acquired > 0 && acquired <= owner.Budget.ReservedBytes - held);
        Assert.Equal(source, value);
    }

    [Theory]
    [InlineData(0ul)] [InlineData(47ul)] [InlineData(48ul)]
    public void TextByteDenialPreservesParentCapacityAndRoots(ulong allowance)
    {
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, allowance + 48, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        var failure = Record.Exception(() => owner.Budget.CopyText("a"));
        if (allowance < 48)
        {
            var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
            Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, breach.ResourceKind);
            Assert.Equal(48ul, owner.Budget.ReservedBytes);

        }
        else { Assert.Null(failure); Assert.Equal(96ul, owner.Budget.ReservedBytes); }
        owner.Budget.ReleaseArray(parent, false);
    }

    [Fact]
    public void BulkGrowthDenialKeepsOriginalCharactersAndCapacity()
    {
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, 207, ulong.MaxValue, ulong.MaxValue);
        using var buffer = Utf8ConstructionBuffer<char>.Create(owner.Budget);
        buffer.AddRange("abcd");
        var held = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => buffer.AddRange("e"));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal("abcd", new string(buffer.Span));
        Assert.Equal(held, owner.Budget.ReservedBytes);
        Assert.True(owner.Budget.PeakBytes <= 207);
    }

    [Theory]
    [InlineData(0ul)] [InlineData(1ul)] [InlineData(2ul)] [InlineData(3ul)]
    public void StringCopyWorkDenialRestoresParentWithoutCreatingRoots(ulong allowance)
    {
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, allowance + 1, ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        using (var child = Utf8ConstructionScope.Enter())
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => child.Budget.CopyText("12345678"));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        }
        Assert.Equal(48ul, owner.Budget.ReservedBytes);

        Assert.Equal(1ul, owner.Budget.WorkSteps);
        owner.Budget.ReleaseArray(parent, false);
    }

    [Fact]
    public void InvariantNumbersAndScalarPairsNeedNoIntermediateStrings()
    {
        using var owner = Utf8ConstructionScope.Enter();
        using var text = Utf8ConstructionText.Create();
        text.Append(int.MinValue).Append('|').Append(int.MaxValue).Append('|').AppendHex(0xbeef).Append('|').AppendScalar(0x1f600);
        Assert.Equal("-2147483648|2147483647|BEEF|😀", text.ToText());
        text.Clear();
        Assert.Equal(0, text.Length);
        Assert.Equal(string.Empty, text.ToText());
    }

    [Fact]
    public void OrdinalReplacementBorrowsUnchangedTextAndAdmitsChangedText()
    {
        using var owner = Utf8ConstructionScope.Enter();
        var source = "a😀a😀b";
        var held = owner.Budget.ReservedBytes;
        Assert.Same(source, Utf8ConstructionText.Replace(source, "x", "a"));
        Assert.Equal(held, owner.Budget.ReservedBytes);
        var result = Utf8ConstructionText.Replace(source, "😀", "!");
        Assert.Equal("a!a!b", result);
        Assert.True(owner.Budget.ReservedBytes > held);
        Assert.Equal("a😀a😀b".Replace("😀", "!", StringComparison.Ordinal), result);
    }

    [Fact]
    public void LiteralEscapingMatchesTheDeclaredGrammarForEveryUtf16Character()
    {
        using var owner = Utf8ConstructionScope.Enter();
        for (var value = 0; value <= 0xffff; value++)
        {
            var character = (char)value;
            Assert.Equal(Regex.Escape(character.ToString()), Utf8ConstructionText.EscapeScalar(value));
        }
        Assert.Equal("😀", Utf8ConstructionText.EscapeScalar(0x1f600));
    }
}
