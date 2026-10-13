using System.Reflection;
using System.Runtime.ExceptionServices;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReCaptureMetadataAdmissionTests
{
    private static readonly IReadOnlyDictionary<string, int> s_names = new Dictionary<string, int>
    { ["third"] = 3, ["first"] = 1 };

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void MetadataCannotBypassTheOriginalWorkOrByteOwner(bool entries, bool work)
    {
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, work ? ulong.MaxValue : 0,
            work ? 0 : ulong.MaxValue, ulong.MaxValue);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Call(entries));
        Assert.Equal(work ? Utf8ResourceLimitKind.ConstructionWorkSteps : Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.WorkSteps);
        Assert.Equal(0ul, owner.Budget.Depth);
    }

    [Fact]
    public void NumericNameDenialRestoresParentStorageWithoutRefundingWork()
    {
        var arrayBytes = Utf8ConstructionBudget.ArrayBytes<string>(4);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, 48 + arrayBytes, ulong.MaxValue, ulong.MaxValue);
        var loan = owner.Budget.AllocateArray<byte>(1, false);
        loan[0] = 37;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Call(false));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.True(owner.Budget.WorkSteps > 1);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(37, loan[0]);
    }

    [Fact]
    public void SparseNamedSlotsKeepNumericNamesAndCallerOwnedCopies()
    {
        var regex = new Utf8PythonRegex("(?P<first>a)(b)(?P<third>c)(d)");
        Assert.Equal(new[] { "0", "first", "2", "third", "4" }, regex.GetGroupNames());
        var first = regex.GetGroupNames(); first[1] = "changed";
        Assert.Equal("first", regex.GetGroupNames()[1]);
        var result = regex.SearchDetailedData("abcd"u8);
        Assert.Null(result.LastGroup);
        Assert.Equal(4, result.LastIndex);
    }

    [Theory]
    [InlineData(9)] [InlineData(10)] [InlineData(99)] [InlineData(100)]
    [InlineData(299)] [InlineData(300)] [InlineData(999)] [InlineData(1000)]
    public void NumericNamesUseInvariantTextAcrossDigitAndCacheBoundaries(int count)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var method = typeof(Utf8PythonRegex).GetMethod("GetPublicGroupNames", BindingFlags.NonPublic | BindingFlags.Static)!;
        var names = Assert.IsType<string[]>(method.Invoke(null, [count, new Dictionary<string, int>()]));
        Assert.Equal(count + 1, names.Length);
        Assert.Equal("0", names[0]);
        for (var number = 1; number <= count; number++)
            Assert.Equal(number.ToString(System.Globalization.CultureInfo.InvariantCulture), names[number]);
        Assert.True(owner.Budget.ReservedBytes > Utf8ConstructionBudget.ArrayBytes<string>(count + 1));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void MetadataUsesOriginalInterruptionAndRollsBackItsArrays(bool entries, bool timeout)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var loan = owner.Budget.AllocateArray<byte>(1, false); loan[0] = 37;
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromSeconds(30));
        owner.Budget.Deadline = deadline; owner.Budget.CancellationToken = cancellation.Token;
        var checkpoint = 0;
        owner.Budget.CheckpointHook = _ => { if (++checkpoint == 2) { if (timeout) ticks = 30000; else cancellation.Cancel(); } };
        var failure = Record.Exception(() => Call(entries));
        if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Same(deadline, owner.Budget.Deadline);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.True(owner.Budget.WorkSteps > 1);
        Assert.Equal(37, loan[0]);
    }

    private static object Call(bool entries)
    {
        var method = typeof(Utf8PythonRegex).GetMethod(entries ? "GetPythonNameEntries" : "GetPublicGroupNames", BindingFlags.NonPublic | BindingFlags.Static)!;
        try { return method.Invoke(null, entries ? [s_names] : [3, s_names])!; }
        catch (TargetInvocationException error) when (error.InnerException is Exception cause)
        { ExceptionDispatchInfo.Capture(cause).Throw(); throw; }
    }
}
