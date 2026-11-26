using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReSplitOwnershipTests
{
    public static IEnumerable<object?[]> Cases()
    {
        var none = PythonReCompileOptions.None;
        var ignoreCase = PythonReCompileOptions.IgnoreCase;
        yield return new object?[] { ":", none, ":a:b::c", 0, 0, new string?[] { "", "a", "b", "", "c" } };
        yield return new object?[] { ":", none, ":a:b::c", 2, 0, new string?[] { "", "a", "b::c" } };
        yield return new object?[] { ":", none, ":a:b::c", 1, 0, new string?[] { "", "a:b::c" } };
        yield return new object?[] { "cat|horse", none, "cat horse fish", 0, 0, new string?[] { "", " ", " fish" } };
        yield return new object?[] { "cat|horse", none, "cathorse", 0, 0, new string?[] { "", "", "" } };
        yield return new object?[] { "cat|horse", none, "no match", 0, 0, new string?[] { "no match" } };
        yield return new object?[] { "cat|horse", none, "", 0, 0, new string?[] { "" } };
        yield return new object?[] { "(cat)", none, "cat horse cat", 0, 0, new string?[] { "", "cat", " horse ", "cat", "" } };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse", 0, 0, new string?[] { "", "cat", null, " ", null, "horse", "" } };
        yield return new object?[] { "(cat)|(horse)", none, "horsecat", 0, 0, new string?[] { "", null, "horse", "", "cat", null, "" } };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse fish horse", 2, 0, new string?[] { "", "cat", null, " ", null, "horse", " fish horse" } };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse", 1, 0, new string?[] { "", "cat", null, " horse" } };
        yield return new object?[] { "(?P<w>cat)|(?P<v>horse)", none, "cat horse", 0, 0, new string?[] { "", "cat", null, " ", null, "horse", "" } };
        yield return new object?[] { "(cat)|horse", none, "horsecat", 0, 0, new string?[] { "", null, "", "cat", "" } };
        yield return new object?[] { "(a)|(ab)", none, "xab", 0, 0, new string?[] { "x", "a", null, "b" } };
        yield return new object?[] { "(ab)|(a)", none, "xab", 0, 0, new string?[] { "x", "ab", null, "" } };
        yield return new object?[] { "((cat)|(horse))", none, "cat horse", 0, 0, new string?[] { "", "cat", "cat", null, " ", "horse", null, "horse", "" } };
        yield return new object?[] { "(?:cat|horse)", none, "cat horse", 0, 0, new string?[] { "", " ", "" } };
        yield return new object?[] { "(CAT)|(DOG)", ignoreCase, "cat dog", 0, 0, new string?[] { "", "cat", null, " ", null, "dog", "" } };
        yield return new object?[] { "(CAT)|(DOG)", ignoreCase, "CAT DOG", 0, 0, new string?[] { "", "CAT", null, " ", null, "DOG", "" } };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse", 0, 4, new string?[] { "", null, "horse", "" } };
        yield return new object?[] { "cat|horse", none, "cat horse", 0, 4, new string?[] { "", "" } };
        yield return new object?[] { "(caf\u00E9)|(ni\u00F1o)", none, "\u03B1caf\u00E9\u03B2ni\u00F1o\u03B3", 0, 0, new string?[] { "\u03B1", "caf\u00E9", null, "\u03B2", null, "ni\u00F1o", "\u03B3" } };
        yield return new object?[] { "(?P<s>😀)|(,)?;", none, "é😀a;🙂,;z", 0, 0, new string?[] { "é", "😀", null, "a", null, null, "🙂", null, ",", "z" } };
        yield return new object?[] { "(?P<s>😀)|(,)?;", none, "é😀a;🙂,;z", 2, 2, new string?[] { "", "😀", null, "a", null, null, "🙂,;z" } };
        yield return new object?[] { "()", none, "😀é", 2, 0, new string?[] { "", "", "😀", "", "é" } };
        yield return new object?[] { "(b)|(:+)", none, ":a:b::c", 0, 0, new string?[] { "", null, ":", "a", null, ":", "", "b", null, "", null, "::", "c" } };
        yield return new object?[] { "(:)+", none, ":a:b::c", 0, 0, new string?[] { "", ":", "a", ":", "b", ":", "c" } };
        yield return new object?[] { "(a)+", none, "aaabaaa", 0, 0, new string?[] { "", "a", "b", "a", "" } };
        yield return new object?[] { "a*", none, "ba", 0, 0, new string?[] { "", "b", "", "" } };
        yield return new object?[] { "(?:b)|(?::+)", none, ":a:b::c", 0, 0, new string?[] { "", "a", "", "", "c" } };
    }

    [Fact]
    public void CaptureSlotsDistinguishUnmatchedFromEmptyAndSurviveProgramDisposal()
    {
        using var regex = new Utf8PythonRegex("(cat)|(horse)");
        var input = "cat horse"u8.ToArray();
        var ranges = regex.SplitRanges(input);
        regex.Dispose();
        Assert.Equal(new string?[] { "", "cat", null, " ", null, "horse", "" },
            PythonReSplitAssert.Decode(input, ranges));
        Assert.Equal(new[] { 0, 1, 2, 0, 1, 2, 0 }, ranges.Select(r => r.Number));
        Assert.Equal(new[] { true, true, false, true, false, true, true }, ranges.Select(r => r.Success));
        Assert.Equal((0, 0), (ranges[0].StartOffsetInBytes, ranges[0].EndOffsetInBytes));
        Assert.Equal((0, 3), (ranges[1].StartOffsetInBytes, ranges[1].EndOffsetInBytes));
        Assert.Equal((4, 9), (ranges[5].StartOffsetInBytes, ranges[5].EndOffsetInBytes));
    }

    [Fact]
    public void SmallCappedSplitAdmitsOnlyItsActualRangeArray()
    {
        var bytes = Utf8ConstructionBudget.ArrayBytes<Utf8PythonCaptureRange>(4);
        var allowance = new PythonReSharedWorkingAllowanceTests.Allowance(4 << 20);
        using var regex = Utf8PythonRegex.Compile("[,;]", options: default, policy: new()
        {
            ProgramMemoryAllowance = allowance,
            ResourcePolicy = new() { MaxResultBytes = bytes, MaxWorkingBytes = bytes }
        });
        var program = allowance.Used;
        var reservations = allowance.Reservations;
        var input = "😀a,b;c,d;e"u8.ToArray();
        allowance.Maximum = program + bytes;
        var result = regex.SplitRanges(input, maxSplit: 3, startOffsetInBytes: 4);
        Assert.Equal(new string?[] { "a", "b", "c", "d;e" }, PythonReSplitAssert.Decode(input, result));
        Assert.Equal(reservations + 1, allowance.Reservations);
        Assert.Equal(program, allowance.Used);

        allowance.Maximum--;
        Assert.Throws<Utf8MemoryAdmissionException>(() => regex.SplitRanges(input, 3, 4));
        Assert.Equal(program, allowance.Used);
        allowance.Maximum++;

        using var source = new CancellationTokenSource();
        allowance.AfterGrant = source.Cancel;
        var failure = Assert.Throws<OperationCanceledException>(() => regex.SplitRanges(input, 3, 4, source.Token));
        Assert.Equal(source.Token, failure.CancellationToken);
        Assert.Equal(program, allowance.Used);
        Assert.Equal(new string?[] { "a", "b", "c", "d;e" }, PythonReSplitAssert.Decode(input, result));
    }
}
