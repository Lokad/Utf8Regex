using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ExecutionArchitectureTests
{
    [Fact]
    public void CompiledProgramUsesTypedOperationBackendsWithOwnedPayloads()
    {
        var delegated = new Utf8Pcre2Regex("abc").DebugCompiledProgram;
        var special = new Utf8Pcre2Regex("(?|(abc)|(xyz))").DebugCompiledProgram;

        Assert.IsType<Pcre2LiteralDirectProgram>(delegated.Operations.IsMatch);
        Assert.IsType<Pcre2Utf8ProgramSlot>(delegated.PrimaryUtf8);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(special.Operations.IsMatch);
        Assert.IsType<Pcre2EmptyUtf8ProgramSlot>(special.PrimaryUtf8);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(special.Operations.Match);
        Assert.Equal(special.Operations.IsMatch, special.CandidateSearch.Program);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(special.Operations.Match);
    }

    [Fact]
    public void InvocationStateIsNeverRetainedByCompiledProgram()
    {
        Assert.DoesNotContain(
            typeof(Pcre2CompiledProgram).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic),
            static field => field.FieldType == typeof(Pcre2InvocationState));

        var first = new Pcre2InvocationState(3, default, Timeout.InfiniteTimeSpan);
        var second = new Pcre2InvocationState(3, default, Timeout.InfiniteTimeSpan);
        Assert.NotSame(first.Captures, second.Captures);
        Assert.NotSame(first.Backtracking, second.Backtracking);
        Assert.NotSame(first.GlobalIteration, second.GlobalIteration);
        Assert.Equal(0UL, first.Budget.CandidateSteps);
        Assert.Equal(0UL, second.Budget.CandidateSteps);
    }

    [Fact]
    public void CompiledRegexCanBeSharedAcrossConcurrentInvocations()
    {
        var regex = new Utf8Pcre2Regex("(?|(abc)|(xyz))");
        var input = Encoding.UTF8.GetBytes("abc xyz abc xyz");
        var failures = 0;

        Parallel.For(
            0,
            256,
            _ =>
            {
                if (!regex.IsMatch(input) || regex.Count(input) != 4)
                {
                    Interlocked.Increment(ref failures);
                }
            });

        Assert.Equal(0, failures);
    }
}
