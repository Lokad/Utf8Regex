using System.Reflection;
using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2CharacterConstructionBoundsTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void CharacterProgramsAdmitInstructionsAndBytesBeforeGrowth(bool bytes, bool memory)
    {
        var pattern = string.Concat(Enumerable.Repeat(@"\X", 1000));
        var policy = memory ? new Utf8ConstructionPolicy { MaxBytes = 16384 }
            : new Utf8ConstructionPolicy { MaxInstructionCapacity = 1 };
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Compile(pattern, policy, bytes));
        Assert.Equal(memory ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionInstructionCapacity,
            failure.ResourceKind);
        Assert.False(Utf8ConstructionScope.IsActive);
        var regex = Compile(pattern, Utf8ConstructionPolicy.Default, bytes);
        Assert.False(regex.DebugUsesUtf8RegexTranslation);
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(new string('a', 1000))));
    }

    [Fact]
    public void SingleTokenClassParsingSharesAdmissionAndRestoresTheParentOnRefusal()
    {
        using var parent = new Utf8ConstructionScope(10000, 128);
        var held = parent.Budget.AllocateArray<byte>(8, false);
        var before = parent.Budget.ReservedBytes;
        var pattern = "[" + new string('a', 1000) + "]";
        Assert.Throws<Utf8ResourceLimitExceededException>(() => Pcre2CharacterCompiler.TryCompileSingleToken(
            new(pattern, default, default, default, Timeout.InfiniteTimeSpan), 0, default, out _, out _));
        Assert.Equal(before, parent.Budget.ReservedBytes);
        Assert.Equal(0ul, parent.Budget.Reserved);
        Assert.Equal(8, held.Length);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CharacterGrowthObservesTheOriginalConstructionInterruption(bool timeout)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        using var parent = new Utf8ConstructionScope(10000, 1000000);
        parent.Budget.CancellationToken = cancellation.Token;
        parent.Budget.Deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(10));
        var interrupted = false;
        parent.Budget.CheckpointHook = _ =>
        {
            if (parent.Budget.Reserved < 64) return;
            interrupted = true;
            if (timeout) ticks = 20; else cancellation.Cancel();
        };
        var pattern = string.Concat(Enumerable.Repeat(@"\X", 1000));
        Exception? failure = Record.Exception(() => Pcre2CharacterCompiler.Compile(
            new(pattern, default, default, default, Timeout.InfiniteTimeSpan)));
        Assert.True(interrupted);
        if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(0ul, parent.Budget.ReservedBytes);
        Assert.Equal(0ul, parent.Budget.Reserved);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SyntaxMetadataDoesNotRetainLinearConstructionVectors(bool character)
    {
        var pattern = character ? string.Concat(Enumerable.Repeat(@"\X", 1000)) : new string('a', 4096);
        var regex = new Utf8Pcre2Regex(pattern);
        Assert.DoesNotContain(ReachableArrays(Assert.IsAssignableFrom<IPcre2SyntaxTree>(regex.DebugCompiledProgram.SyntaxTree)), array => array.Length >= 1000);
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(new string('a', character ? 1000 : 4096))));
    }

    private static IEnumerable<Array> ReachableArrays(object root)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<object>();
        pending.Push(root);
        while (pending.TryPop(out var item))
        {
            if (!seen.Add(item)) continue;
            if (item is Array array) { yield return array; continue; }
            if (item.GetType().Assembly != typeof(Utf8Pcre2Regex).Assembly) continue;
            foreach (var field in item.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                if (field.GetValue(item) is { } child) pending.Push(child);
        }
    }

    private static Utf8Pcre2Regex Compile(string pattern, Utf8ConstructionPolicy policy, bool bytes)
        => bytes ? Utf8Pcre2Regex.Compile(Encoding.UTF8.GetBytes(pattern), default, default,
            Timeout.InfiniteTimeSpan, Utf8ResourcePolicy.Unlimited, policy, Timeout.InfiniteTimeSpan, default)
            : Utf8Pcre2Regex.Compile(pattern, default, default, Timeout.InfiniteTimeSpan,
                Utf8ResourcePolicy.Unlimited, policy, Timeout.InfiniteTimeSpan, default);
}
