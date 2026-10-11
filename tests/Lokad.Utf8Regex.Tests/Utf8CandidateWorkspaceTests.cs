using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8CandidateWorkspaceTests
{
    [Theory]
    [InlineData(3)] [InlineData(17)] [InlineData(257)]
    public void PortfolioAndVerifierShareOneAggregateWorkingPeak(int sources)
    {
        var runtime=Runtime("😀");var input=Encoding.UTF8.GetBytes("x😀x");var validation=Utf8Validation.Validate(input);
        var direct=Invocation(1024*1024);Assert.True(direct.Ledger.TryReserve(7));
        Assert.Equal(Utf8ExecutionLimitKind.None,runtime.TryVerifyCandidate(input,
            Utf8StructuralCandidate.Start(1,5,5,0),validation,ref direct,out var expected));
        Assert.True(expected.Success);Assert.Equal(7ul,direct.Ledger.ReservedBytes);
        var combined=Invocation(1024*1024);Assert.True(combined.Ledger.TryReserve(7));
        Assert.True(Utf8FallbackSearchExecutor.TryFindNextVerifiedMatch(Plans(sources,"😀"),runtime,input,validation,0,
            ref combined,out var attempts,out var actual));
        Assert.Equal(expected,actual);Assert.Equal(1,attempts);
        Assert.Equal(PortfolioBytes(sources)+direct.Budget.PeakWorkingBytes,combined.Budget.PeakWorkingBytes);
        Assert.Equal(7ul,combined.Ledger.ReservedBytes);
    }

    [Fact]
    public void VerifierScratchIncludesBothPhysicalArrayHeaders()
    {
        var invocation=Invocation(1024*1024);var runtime=Runtime("(?<x>a)");
        Assert.Equal(Utf8ExecutionLimitKind.None,runtime.TryVerifyCandidate("a"u8,
            Utf8StructuralCandidate.Start(0,1,1,0),Utf8Validation.Validate("a"u8),ref invocation,out var result));
        Assert.True(result.Success);
        Assert.True(invocation.Budget.PeakWorkingBytes>=2*Utf8ConstructionBudget.ArrayBytes<int>(1));
        Assert.Equal(0ul,invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(17)] [InlineData(257)]
    public void ExactCapacityAdmissionPrecedesEveryPortfolioAcquisition(int sources)
    {
        Utf8ExactStatePool.Clear();
        try
        {
            var plans=Plans(sources,"a");var bytes=PortfolioBytes(sources);var parent=7ul;
            foreach(var maximum in bytes==0?new[]{parent}:new[]{parent+bytes-1,parent+bytes})
            {
                var invocation=Invocation(maximum);Assert.True(invocation.Ledger.TryReserve(parent));
                InvalidOperationException? failure=null;
                try
                {
                    using var cursor=new Utf8CandidatePortfolioCursor(plans,"a"u8,0,ref invocation);
                    Assert.Equal(parent+bytes,invocation.Ledger.ReservedBytes);
                    Assert.Equal(sources>2,cursor.UsesPooledStorage);
                }
                catch(InvalidOperationException error){failure=error;}
                if(maximum<parent+bytes)Assert.Contains("WorkingBytes",Assert.IsType<InvalidOperationException>(failure).Message);
                else Assert.Null(failure);
                Assert.Equal(parent,invocation.Ledger.ReservedBytes);Assert.Equal(0,Utf8ExactStatePool.RetainedBytes);
            }
        }
        finally{Utf8ExactStatePool.Clear();}
    }

    [Fact]
    public void EveryPortfolioWorkCutoffRestoresParentLoansAndKeepsSpentWork()
    {
        var plans=Plans(17,"a");var probe=new Utf8SliceInvocation(new(ulong.MaxValue,0,0),Regex.InfiniteMatchTimeout,default);
        using(var cursor=new Utf8CandidatePortfolioCursor(plans,"aaa"u8,0,ref probe))
            while(cursor.TryGetNextScalarBoundary(out _)){}
        var required=probe.Budget.Steps;Assert.True(required>64);
        for(var cutoff=1ul;cutoff<=required;cutoff++)
        {
            var invocation=new Utf8SliceInvocation(new(cutoff,0,0),Regex.InfiniteMatchTimeout,default);
            Assert.True(invocation.Ledger.TryReserve(7));InvalidOperationException? failure=null;
            try
            {
                using var cursor=new Utf8CandidatePortfolioCursor(plans,"aaa"u8,0,ref invocation);
                while(cursor.TryGetNextScalarBoundary(out _)){}
            }
            catch(InvalidOperationException error){failure=error;}
            if(cutoff<required)
            {Assert.Contains("WorkSteps",Assert.IsType<InvalidOperationException>(failure).Message);Assert.Equal(cutoff+1,invocation.Budget.Steps);}
            else Assert.Null(failure);
            Assert.Equal(7ul,invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CancellationDuringInitializationOrAdvanceUnwindsTheSameScope(bool initialization)
    {
        using var cancellation=new CancellationTokenSource();
        var invocation=new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited,Regex.InfiniteMatchTimeout,cancellation.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        invocation.CheckpointHook=_=>cancellation.Cancel();
        OperationCanceledException? failure=null;
        try
        {
            using var cursor=new Utf8CandidatePortfolioCursor(Plans(initialization?67:3,"a"),"aaaaaaaa"u8,0,ref invocation);
            while(cursor.TryGetNextScalarBoundary(out _)){}
        }
        catch(OperationCanceledException error){failure=error;}
        Assert.Equal(cancellation.Token,Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(7ul,invocation.Ledger.ReservedBytes);Assert.True(invocation.Budget.Steps>=64);
    }

    [Fact]
    public void FiniteCallsBorrowButNeverGrowSharedRetention()
    {
        Utf8ExactStatePool.Clear();
        try
        {
            var plans=Plans(3,"a");var warm=new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited,Regex.InfiniteMatchTimeout,default);
            using(var cursor=new Utf8CandidatePortfolioCursor(plans,"a"u8,0,ref warm))Assert.True(cursor.TryGetNext(out _));
            var retained=Utf8ExactStatePool.RetainedBytes;Assert.True(retained>0);
            var finite=Invocation(1024*1024);
            using(var cursor=new Utf8CandidatePortfolioCursor(plans,"a"u8,0,ref finite))Assert.True(cursor.TryGetNext(out _));
            Assert.InRange(Utf8ExactStatePool.RetainedBytes,0,retained);Assert.Equal(0ul,finite.Ledger.ReservedBytes);
        }
        finally{Utf8ExactStatePool.Clear();}
    }

    [Fact]
    public void NoCaptureSlotsUseSharedEmptiesWithoutPerAttemptAllocation()
    {
        var runtime=Runtime("a");var invocation=new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited,Regex.InfiniteMatchTimeout,default);
        var candidate=Utf8StructuralCandidate.Start(0,1,1,0);var validation=Utf8Validation.Validate("a"u8);
        for(var index=0;index<64;index++)runtime.TryVerifyCandidate("a"u8,candidate,validation,ref invocation,out _);
        var before=GC.GetAllocatedBytesForCurrentThread();
        var correct=true;
        for(var index=0;index<256;index++)
        {correct &= runtime.TryVerifyCandidate("a"u8,candidate,validation,ref invocation,out var result)==Utf8ExecutionLimitKind.None&&result.Success;}
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.True(correct);Assert.Equal(0,allocated);Assert.Equal(0ul,invocation.Ledger.ReservedBytes);
    }

    internal static ulong PortfolioBytes(int sources)
    {
        if(sources<=2)return 0;
        var capacity=sources<=256?(int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(16,sources)):sources;
        return checked(Utf8ConstructionBudget.ArrayBytes<Utf8StructuralSearchState>(capacity)+
            Utf8ConstructionBudget.ArrayBytes<Utf8StructuralCandidate>(capacity)+Utf8ConstructionBudget.ArrayBytes<bool>(capacity));
    }
    internal static Utf8SliceInvocation Invocation(ulong bytes)=>new(new(0,0,bytes),Regex.InfiniteMatchTimeout,default);
    internal static Utf8VerifierRuntime Runtime(string pattern)=>Utf8VerifierRuntime.Create(
        Utf8ExecutionRetainedGraphTests.Prepared(new Utf8Regex(pattern,RegexOptions.CultureInvariant)));
    internal static Utf8StructuralSearchPlan[] Plans(int count,string literal)
    {
        var searcher=new PreparedSearcher(new PreparedSubstringSearch(Encoding.UTF8.GetBytes(literal),ignoreCase:false),ignoreCase:false);
        return Enumerable.Range(0,count).Select(_=>Utf8StructuralSearchPlan.CreateStartPlan(searcher,exactLength:Encoding.UTF8.GetByteCount(literal))).ToArray();
    }
}
