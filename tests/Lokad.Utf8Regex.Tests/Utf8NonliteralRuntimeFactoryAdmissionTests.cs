using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Xunit;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NonliteralRuntimeFactoryAdmissionTests
{
    private static Utf8PreparedRegex Plan(string pattern) => (Utf8PreparedRegex)typeof(Utf8Regex)
        .GetProperty("PreparedRegex", BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetValue(new Utf8Regex(pattern, RegexOptions.CultureInvariant))!;

    public static IEnumerable<object[]> Roots() => from kind in Enum.GetValues<Utf8CompiledEngineKind>()
        where kind == Utf8CompiledEngineKind.StructuralLinearAutomaton
        from limit in new[] { "bytes", "work", "depth" } select new object[] { (int)kind, limit };

    private static Utf8CompiledEngineRuntime Build(int kind, Utf8PreparedRegex plan, Utf8VerifierRuntime verifier) =>
        Utf8CompiledRuntimeFactory.Create(new Utf8CompiledEngine((Utf8CompiledEngineKind)kind), plan, verifier, RegexOptions.None);

    [Theory]
    [MemberData(nameof(Roots))]
    public void EveryStorageWorkAndDepthCutoffUnwindsChildrenWithoutRefundingWork(int kind, string limit)
    {
        const string pattern = "(?:call|invoke)[ \t]+[A-Z][a-z]*\\(";
        var plan = Plan(pattern);
        var verifier = Utf8VerifierRuntime.Create(plan);
        ulong peak, work, depth, loan;
        using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            _ = funded.Budget.AllocateObject(17, static v => new Box(v));
            _ = Build(kind, plan, verifier);
            peak = funded.Budget.PeakBytes - 128; work = funded.Budget.WorkSteps - 2;
            depth = funded.Budget.PeakDepth; loan = funded.Budget.ReservedBytes;
        }
        var maximum = limit == "bytes" ? peak : limit == "work" ? work : depth;
        var successes = 0;
        for (var allowance = 0ul; allowance <= maximum; allowance++)
        {
            using var owner = new Utf8ConstructionScope(0, limit == "bytes" ? 128 + allowance : ulong.MaxValue,
                limit == "work" ? 2 + allowance : ulong.MaxValue, limit == "depth" ? allowance : ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static v => new Box(v));
            var checkpoint = owner.Budget.ReservedBytes;
            var error = Record.Exception(() => _ = Build(kind, plan, verifier));
            if (error is null)
            {
                successes++; Assert.Equal(loan, owner.Budget.ReservedBytes);
            }
            else
            {
                var expected = limit == "bytes" ? Utf8ResourceLimitKind.ConstructionBytes :
                    limit == "work" ? Utf8ResourceLimitKind.ConstructionWorkSteps : Utf8ResourceLimitKind.ConstructionDepth;
                Assert.Equal(expected, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            }
            Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, limit == "work" ? 2 + allowance : 2 + work);
            if (allowance == 0 && limit == "depth")
            { Assert.Equal(128ul, owner.Budget.PeakBytes); Assert.Equal(2ul, owner.Budget.WorkSteps); }
        }
        Assert.Equal(1, successes);
    }

    private sealed class Box(int value) { internal readonly int Value = value; }
}
