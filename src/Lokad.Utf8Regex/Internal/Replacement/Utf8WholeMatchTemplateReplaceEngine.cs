using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal static class Utf8WholeMatchTemplateReplaceEngine
{
    public delegate bool TryFindNextMatch(ReadOnlySpan<byte> input, int startIndex, out int matchIndex, out int matchLength);

    public static byte[] Replace(
        ReadOnlySpan<byte> input,
        Utf8ReplacementPlan plan,
        TryFindNextMatch tryFindNextMatch,
        Utf8ExecutionDeadline budget)
    {
        var template = CreateTemplate(plan);
        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            var outputLength = BuildRanges(input, template, tryFindNextMatch, budget, ref ledger);
            if (ledger.Count == 0)
            {
                return input.ToArray();
            }

            var output = new byte[outputLength.Value];
            Emit(input, template, output, ledger.WrittenRanges);
            return output;
        }
        finally
        {
            ledger.Dispose();
        }
    }

    public static bool TryReplace(
        ReadOnlySpan<byte> input,
        Utf8ReplacementPlan plan,
        TryFindNextMatch tryFindNextMatch,
        Span<byte> destination,
        out int bytesWritten,
        Utf8ExecutionDeadline budget)
    {
        var template = CreateTemplate(plan);
        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            var outputLength = BuildRanges(input, template, tryFindNextMatch, budget, ref ledger);
            if (ledger.Count == 0)
            {
                return Utf8ReplacementOutput.TryCopyUnchanged(input, destination, out bytesWritten);
            }

            if (outputLength.Value > destination.Length)
            {
                bytesWritten = 0;
                return false;
            }

            Emit(input, template, destination, ledger.WrittenRanges);
            bytesWritten = outputLength.Value;
            return true;
        }
        finally
        {
            ledger.Dispose();
        }
    }

    public static bool CanExecute(Utf8ReplacementPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        foreach (var instruction in plan.Instructions)
        {
            if (instruction.Kind != Utf8ReplacementInstructionKind.Literal &&
                instruction.Kind != Utf8ReplacementInstructionKind.WholeMatch &&
                !(instruction.Kind == Utf8ReplacementInstructionKind.Group && instruction.GroupNumber == 0))
            {
                return false;
            }
        }

        return true;
    }

    private static Template CreateTemplate(Utf8ReplacementPlan plan)
    {
        var segments = new Segment[plan.Instructions.Count];
        var literalBytesPerMatch = 0;
        var wholeMatchCopiesPerMatch = 0;
        for (var i = 0; i < plan.Instructions.Count; i++)
        {
            var instruction = plan.Instructions[i];
            switch (instruction.Kind)
            {
                case Utf8ReplacementInstructionKind.Literal:
                    var literal = instruction.LiteralUtf8 ?? [];
                    segments[i] = new Segment(literal, CopyWholeMatch: false);
                    literalBytesPerMatch = checked(literalBytesPerMatch + literal.Length);
                    break;

                case Utf8ReplacementInstructionKind.WholeMatch:
                case Utf8ReplacementInstructionKind.Group when instruction.GroupNumber == 0:
                    segments[i] = new Segment([], CopyWholeMatch: true);
                    wholeMatchCopiesPerMatch++;
                    break;

                default:
                    throw new InvalidOperationException("Unsupported whole-match template instruction.");
            }
        }

        return new Template(segments, literalBytesPerMatch, wholeMatchCopiesPerMatch);
    }

    private static Utf8ReplacementOutputLength BuildRanges(
        ReadOnlySpan<byte> input,
        Template template,
        TryFindNextMatch tryFindNextMatch,
        Utf8ExecutionDeadline budget,
        ref Utf8ReplacementRangeLedger ledger)
    {
        var outputLength = new Utf8ReplacementOutputLength(input.Length);
        var nextStart = 0;
        while (nextStart <= input.Length)
        {
            budget.Step();
            if (!tryFindNextMatch(input, nextStart, out var matchIndex, out var matchLength))
            {
                break;
            }

            ledger.Add(new Utf8ReplacementRange(matchIndex, matchLength));
            var emittedLength = checked(
                template.LiteralBytesPerMatch + template.WholeMatchCopiesPerMatch * matchLength);
            outputLength.ReplaceRange(matchLength, emittedLength);
            nextStart = matchIndex + Math.Max(matchLength, 1);
        }

        return outputLength;
    }

    private static void Emit(
        ReadOnlySpan<byte> input,
        Template template,
        Span<byte> destination,
        ReadOnlySpan<Utf8ReplacementRange> matches)
    {
        var sourcePosition = 0;
        var sink = new Utf8ReplacementOutputSink(destination);
        foreach (var match in matches)
        {
            sink.AppendSlice(input, sourcePosition, match.Start - sourcePosition);
            foreach (var segment in template.Segments)
            {
                if (segment.CopyWholeMatch)
                {
                    sink.AppendSlice(input, match.Start, match.Length);
                }
                else
                {
                    sink.Append(segment.LiteralUtf8);
                }
            }

            sourcePosition = match.Start + match.Length;
        }

        sink.Append(input[sourcePosition..]);
    }

    private readonly record struct Segment(byte[] LiteralUtf8, bool CopyWholeMatch);

    private readonly record struct Template(
        Segment[] Segments,
        int LiteralBytesPerMatch,
        int WholeMatchCopiesPerMatch);
}
