using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal static class Utf8FixedTemplateReplaceEngine
{
    public static byte[] Replace(
        ReadOnlySpan<byte> input,
        Utf8FixedTemplateReplacement template,
        Utf8StructuralLinearProgram structuralLinearProgram,
        Utf8ExecutionDeadline budget)
    {
        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            var outputLength = BuildStructuralRanges(input, template, structuralLinearProgram, budget, ref ledger);
            return EmitAllocated(input, template, outputLength, ref ledger);
        }
        finally
        {
            ledger.Dispose();
        }
    }

    public static bool TryReplace(
        ReadOnlySpan<byte> input,
        Utf8FixedTemplateReplacement template,
        Utf8StructuralLinearProgram structuralLinearProgram,
        Span<byte> destination,
        out int bytesWritten,
        Utf8ExecutionDeadline budget)
    {
        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            var outputLength = BuildStructuralRanges(input, template, structuralLinearProgram, budget, ref ledger);
            return TryEmit(input, template, outputLength, destination, ref ledger, out bytesWritten);
        }
        finally
        {
            ledger.Dispose();
        }
    }

    public static byte[] Replace(
        ReadOnlySpan<byte> input,
        Utf8FixedTemplateReplacement template,
        Func<ReadOnlySpan<byte>, int> findFirst,
        Func<ReadOnlySpan<byte>, int, int> findNext,
        Utf8ExecutionDeadline budget)
    {
        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            var outputLength = BuildFixedRanges(input, template, findFirst, findNext, budget, ref ledger);
            return EmitAllocated(input, template, outputLength, ref ledger);
        }
        finally
        {
            ledger.Dispose();
        }
    }

    public static bool TryReplace(
        ReadOnlySpan<byte> input,
        Utf8FixedTemplateReplacement template,
        Func<ReadOnlySpan<byte>, int> findFirst,
        Func<ReadOnlySpan<byte>, int, int> findNext,
        Span<byte> destination,
        out int bytesWritten,
        Utf8ExecutionDeadline budget)
    {
        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            var outputLength = BuildFixedRanges(input, template, findFirst, findNext, budget, ref ledger);
            return TryEmit(input, template, outputLength, destination, ref ledger, out bytesWritten);
        }
        finally
        {
            ledger.Dispose();
        }
    }

    private static Utf8ReplacementOutputLength BuildStructuralRanges(
        ReadOnlySpan<byte> input,
        Utf8FixedTemplateReplacement template,
        Utf8StructuralLinearProgram structuralLinearProgram,
        Utf8ExecutionDeadline budget,
        ref Utf8ReplacementRangeLedger ledger)
    {
        var outputLength = new Utf8ReplacementOutputLength(input.Length);
        var searchStart = 0;
        while (searchStart <= input.Length)
        {
            var matchIndex = Utf8AsciiFixedTokenLinearExecutor.FindNext(
                structuralLinearProgram,
                input,
                searchStart,
                budget,
                out var matchLength);
            if (matchIndex < 0)
            {
                break;
            }

            ledger.Add(new Utf8ReplacementRange(matchIndex, matchLength));
            outputLength.ReplaceRange(matchLength, template.ReplacementLength);
            searchStart = matchIndex + Math.Max(matchLength, 1);
        }

        return outputLength;
    }

    private static Utf8ReplacementOutputLength BuildFixedRanges(
        ReadOnlySpan<byte> input,
        Utf8FixedTemplateReplacement template,
        Func<ReadOnlySpan<byte>, int> findFirst,
        Func<ReadOnlySpan<byte>, int, int> findNext,
        Utf8ExecutionDeadline budget,
        ref Utf8ReplacementRangeLedger ledger)
    {
        var outputLength = new Utf8ReplacementOutputLength(input.Length);
        budget.Step();
        var currentMatch = findFirst(input);
        while (currentMatch >= 0)
        {
            budget.Step();
            ledger.Add(new Utf8ReplacementRange(currentMatch, template.MatchLength));
            outputLength.ReplaceRange(template.MatchLength, template.ReplacementLength);

            var nextStart = currentMatch + template.MatchLength;
            currentMatch = nextStart <= input.Length - template.MatchLength
                ? findNext(input, nextStart)
                : -1;
        }

        return outputLength;
    }

    private static byte[] EmitAllocated(
        ReadOnlySpan<byte> input,
        Utf8FixedTemplateReplacement template,
        Utf8ReplacementOutputLength outputLength,
        ref Utf8ReplacementRangeLedger ledger)
    {
        if (ledger.Count == 0)
        {
            return input.ToArray();
        }

        var output = new byte[outputLength.Value];
        Emit(input, template, output, ledger.WrittenRanges);
        return output;
    }

    private static bool TryEmit(
        ReadOnlySpan<byte> input,
        Utf8FixedTemplateReplacement template,
        Utf8ReplacementOutputLength outputLength,
        Span<byte> destination,
        ref Utf8ReplacementRangeLedger ledger,
        out int bytesWritten)
    {
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

    private static void Emit(
        ReadOnlySpan<byte> input,
        Utf8FixedTemplateReplacement template,
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
                switch (segment.Kind)
                {
                    case Utf8FixedTemplateReplacementSegmentKind.Literal:
                        if (segment.LiteralUtf8 is { Length: > 0 } literalUtf8)
                        {
                            sink.Append(literalUtf8);
                        }
                        break;

                    case Utf8FixedTemplateReplacementSegmentKind.MatchSlice:
                        sink.AppendSlice(input, match.Start + segment.MatchOffset, segment.Length);
                        break;
                }
            }

            sourcePosition = match.Start + match.Length;
        }

        sink.Append(input[sourcePosition..]);
    }
}
