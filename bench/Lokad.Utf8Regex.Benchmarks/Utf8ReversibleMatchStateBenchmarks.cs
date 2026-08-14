using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public class Utf8ReversibleMatchStateBenchmarks
{
    private readonly byte[] _input = Enumerable.Repeat((byte)'a', 4096).Append((byte)'z').ToArray();
    private readonly Utf8CaptureSlots _captures = new(2);
    private readonly Utf8ExecutionProgram _program =
        Utf8FrontEnd.Compile("((?:a|b){0,2147483647})z", RegexOptions.CultureInvariant).ExecutionProgram ??
        throw new InvalidOperationException("The reversible-state benchmark pattern did not produce an execution program.");

    [Benchmark]
    public int QuantifiedCaptureWithHugeSyntacticBound()
    {
        Utf8ExecutionInterpreter.TryMatchPrefix(
            _input,
            _program,
            0,
            _captures,
            Utf8ExecutionDeadline.Infinite,
            out var matchedLength);
        return matchedLength;
    }
}
