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

    [Theory]
    [InlineData(256)] [InlineData(1024)] [InlineData(4096)]
    public void DenseOrdinalReplacementFitsLinearConstructionWork(int count)
    {
        var source = string.Concat(Enumerable.Repeat(@"\ ", count));
        var allowance = 16ul * (ulong)source.Length;
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, allowance, ulong.MaxValue);
        Assert.Equal(new string(' ', count), Utf8ConstructionText.Replace(source, @"\ ", " "));
        Assert.InRange(owner.Budget.WorkSteps, (ulong)source.Length, allowance);
    }

    [Theory]
    [InlineData("", "x", "!")]
    [InlineData("aa", "aaa", "!")]
    [InlineData("aaaaa", "aa", "!")]
    [InlineData("ababa", "aba", "!")]
    [InlineData("x\\ \\ y\\ ", @"\ ", " ")]
    [InlineData("😀é😀", "😀", "")]
    [InlineData("aAaA", "a", "expanded😀")]
    public void ReplacementPreservesOrdinalNonoverlappingOffsetsAndTails(string source, string value, string replacement)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var result = Utf8ConstructionText.Replace(source, value, replacement);
        Assert.Equal(source.Replace(value, replacement, StringComparison.Ordinal), result);
        if (!source.Contains(value, StringComparison.Ordinal)) Assert.Same(source, result);
    }

    [Fact]
    public void EmptyNeedleIsRejectedWithoutConstructionWork()
    {
        using var owner = Utf8ConstructionScope.Enter();
        Assert.Throws<ArgumentException>(() => Utf8ConstructionText.Replace("abc", "", "!"));
        Assert.Equal(0ul, owner.Budget.WorkSteps);
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ReplacementDenialRestoresParentStorageWithoutRefundingWork(bool bytes)
    {
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, bytes ? 48ul : ulong.MaxValue,
            bytes ? ulong.MaxValue : 16ul, ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        parent[0] = 37;
        using (var child = Utf8ConstructionScope.Enter())
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8ConstructionText.Replace("abababab", "ab", "larger"));
            Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        }
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(37, parent[0]);
        Assert.Equal(bytes ? 17ul : 1ul, owner.Budget.WorkSteps);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ReplacementChecksOriginalInterruptionBetweenSearches(bool timeout)
    {
        using var owner = Utf8ConstructionScope.Enter();
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Lokad.Utf8Regex.Internal.Execution.Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var deadline = Lokad.Utf8Regex.Internal.Execution.Utf8ExecutionDeadline.Start(TimeSpan.FromSeconds(30));
        owner.Budget.Deadline = deadline;
        owner.Budget.CancellationToken = cancellation.Token;
        // Four search units + four helper units + one allocation unit + one
        // copied replacement unit. The next checkpoint resumes the search.
        owner.Budget.CheckpointHook = spent =>
        {
            if (spent != 10) return;
            if (timeout) ticks = 30000;
            else cancellation.Cancel();
        };
        using (var child = Utf8ConstructionScope.Enter())
        {
            var failure = Record.Exception(() => Utf8ConstructionText.Replace("aaaa", "a", "!"));
            if (timeout) Assert.IsType<Lokad.Utf8Regex.Internal.Execution.Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.IsType<OperationCanceledException>(failure);
        }
        Assert.Same(deadline, owner.Budget.Deadline);
        Assert.Equal(10ul, owner.Budget.WorkSteps);
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.True(owner.Budget.WorkSteps > 0);
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
