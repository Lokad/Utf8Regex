using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RepeatProofPredicateTests
{
    [Theory]
    [InlineData(false, false, false, "   7")]
    [InlineData(false, false, false, "\u2003 7")]
    [InlineData(false, false, false, "x7")]
    [InlineData(true, false, false, "abc77")]
    [InlineData(false, true, false, "abc77")]
    [InlineData(true, true, false, " 7")]
    [InlineData(false, false, true, "   7")]
    public void PredicateOrderWorkAndResultsAgreeWithGenericTables(
        bool tableNegated, bool outerNegated, bool folded, string text)
    {
        var direct = Run(false, tableNegated, outerNegated, folded, text, 100000);
        var generic = Run(true, tableNegated, outerNegated, folded, text, 100000);
        Assert.Equal(generic.Found, direct.Found);
        Assert.Equal(generic.End, direct.End);
        Assert.Equal(generic.Breach, direct.Breach);
        Assert.Equal(generic.Work, direct.Work);
        Assert.Equal(generic.Calls, direct.Calls);
        Assert.Equal(0UL, direct.Reserved);
        Assert.Equal(0UL, generic.Reserved);
    }

    [Theory]
    [InlineData(1UL)] [InlineData(5UL)] [InlineData(12UL)] [InlineData(32UL)]
    public void ProofDenialKeepsExactWorkAndPredicateOrder(ulong limit)
    {
        var direct = Run(false, false, false, false, "   7", limit);
        var generic = Run(true, false, false, false, "   7", limit);
        Assert.Equal((generic.Found, generic.End, generic.Breach, generic.Work),
            (direct.Found, direct.End, direct.Breach, direct.Work));
        Assert.Equal(generic.Calls, direct.Calls);
        Assert.Equal(0UL, direct.Reserved);
        Assert.Equal(0UL, generic.Reserved);
    }

    private static (bool Found, int End, Utf8ExecutionLimitKind Breach, ulong Work, int[] Calls, ulong Reserved)
        Run(bool generic, bool tableNegated, bool outerNegated, bool folded, string text, ulong limit)
    {
        var calls = new List<int>();
        var mask = new byte[16];
        foreach (var value in new[] { ' ', '\t', '\n', '\r', '\f' }) mask[value >> 3] |= (byte)(1 << (value & 7));
        var digits = new byte[16];
        for (var value = '0'; value <= '9'; value++) digits[value >> 3] |= (byte)(1 << (value & 7));
        var table = Utf8SliceCategoryTable.ForScalarPredicate(value =>
        {
            calls.Add(value);
            return Rune.IsWhiteSpace(new Rune(value));
        });
        var first = Utf8SliceNode.CategorySetMember.Create(table, tableNegated);
        // An extra false table preserves the language and uses the generic
        // member dispatcher. The first predicate's observable order stays equal.
        Utf8SliceNode.CategorySetMember[] members = generic
            ? [first, Utf8SliceNode.CategorySetMember.Create(Utf8SliceCategoryTable.ForScalarPredicate(static _ => false), false)]
            : [first];
        var atom = new Utf8SliceNode.CategorySetNode(mask, outerNegated, [], members, folded);
        var root = new Utf8SliceNode.ConcatNode([
            new Utf8SliceNode.RepeatNode(atom, 1, long.MaxValue, false),
            new Utf8SliceNode.SetNode(digits, false, [], false),
        ]);
        var program = Utf8SliceLowerer.Lower(root, null, false, false, false)!;
        var input = Encoding.UTF8.GetBytes(text);
        var invocation = new Utf8SliceInvocation(new(limit, 4096, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Utf8Validation.ThrowIfInvalidOnly(input);
        var result = Utf8SliceRunner.TryMatchAt(program, input, 0, [], [], false, true, ref invocation);
        return (result.IsMatch, result.EndIndex, result.Breach, invocation.Budget.Steps, calls.ToArray(), invocation.Ledger.ReservedBytes);
    }
}
