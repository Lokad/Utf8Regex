using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReScalarCompilerContinuationTests
{
    public static IEnumerable<object[]> DeepTrees()
    {
        foreach (var depth in new[] { 129, 16385 })
            foreach (var shape in Enumerable.Range(0, 4))
                foreach (var full in new[] { false, true })
                    yield return [depth, shape, full];
    }

    [Theory]
    [MemberData(nameof(DeepTrees))]
    public void DeepResolvedTreesKeepScalarWidthsWithoutRecursiveRecordHashing(int depth, int shape, bool full)
    {
        var parse = new PythonReParser("(?P<x>.)").Parse(PythonReCompileOptions.None);
        _ = PythonReTranslator.Translate(parse, false);
        var node = parse.Root;
        for (var level = 0; level < depth; level++)
            node = shape switch
            {
                0 => new PythonReGroupNode(PythonReGroupKind.NonCapturing, node, null,
                    PythonReCompileOptions.None, PythonReCompileOptions.None),
                1 => new PythonReGroupNode(PythonReGroupKind.Atomic, node, null,
                    PythonReCompileOptions.None, PythonReCompileOptions.None),
                2 => new PythonReQuantifierNode(node, 1, 1, PythonReQuantifierFlavor.Greedy),
                _ => new PythonReGroupNode(PythonReGroupKind.ScopedFlags, node, null,
                    PythonReCompileOptions.Multiline, PythonReCompileOptions.None),
            };
        var program = PythonReScalarCompiler.Compile(parse with { Root = node });
        if (full) program = PythonReScalarCompiler.RequireFullMatch(program);
        Assert.NotNull(program);
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited,
            Regex.InfiniteMatchTimeout, CancellationToken.None);
        var outcome = Utf8SliceRunner.TryFindFirst(program, "\U0001f600"u8, 0, starts, ends,
            collectHistory: false, ref invocation);
        Assert.True(outcome.Found);
        Assert.Equal((0, 4), (starts[0], ends[0]));
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(65, false)]
    [InlineData(65, true)]
    [InlineData(257, false)]
    [InlineData(257, true)]
    public void PublicDeepCapturesUseScalarCompilerWithoutManagedExecution(int depth, bool scoped)
    {
        var pattern = string.Concat(Enumerable.Repeat(scoped ? "(?i:" : "(", depth)) +
            "(?P<x>.)" + new string(')', depth);
        var regex = new Utf8PythonRegex(pattern);
        var input = Encoding.UTF8.GetBytes("\U0001f600");
        Utf8PythonRangeMatch result = default;
        using (var captureCursor1 = regex.CreateMatchCursor(input, mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor1.MoveNext(out result);
        }
        Assert.True(result.Success);
        Assert.Equal((0, 4), (result.Value.StartOffsetInBytes, result.Value.EndOffsetInBytes));
        Assert.True(regex.TryGetGroupNumber("x"u8, out var groupNumber));
        Assert.Equal(scoped ? 1 : depth + 1, groupNumber);
        Assert.Equal("\U0001f600", System.Text.Encoding.UTF8.GetString((result.Groups[scoped ? 1 : depth + 1].Success ? result.GetGroupMemory(result.Groups[scoped ? 1 : depth + 1].Number).Span : ReadOnlySpan<byte>.Empty)));
    }
}
