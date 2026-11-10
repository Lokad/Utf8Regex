using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Input;

internal static partial class Utf8Validation
{
    internal static void ValidatePolicy(Utf8InputValidation inputValidation)
    {
        if (inputValidation is not Utf8InputValidation.Validate and not Utf8InputValidation.AssumeValid)
            throw new ArgumentOutOfRangeException(nameof(inputValidation));
    }

    // Subject policy never applies to pattern, template or assembled-output validation.
    internal static Utf8ValidationResult Subject(ReadOnlySpan<byte> input, Utf8InputValidation policy)
    {
        if (policy == Utf8InputValidation.AssumeValid) return DescribeValid(input);
        Diagnostics.Utf8SearchDiagnosticsSession.Current?.CountSubjectValidation();
        return Validate(input);
    }

    internal static void CheckSubject(ReadOnlySpan<byte> input, Utf8InputValidation policy)
    {
        if (policy == Utf8InputValidation.Validate)
        {
            Diagnostics.Utf8SearchDiagnosticsSession.Current?.CountSubjectValidation();
            ThrowIfInvalidOnly(input);
        }
    }

    internal static Utf8ValidationResult DescribeValid(ReadOnlySpan<byte> input)
    {
        var ascii = Utf8InputAnalyzer.IsAscii(input);
        return new(input.Length, ascii ? input.Length : Encoding.UTF8.GetCharCount(input), ascii,
            !ascii && input.IndexOfAnyInRange((byte)0xf0, (byte)0xf4) >= 0);
    }

    internal static Utf8ValidationResult Subject(ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation)
    {
        var breach = TrySubject(input, ref invocation, out var result);
        if (breach != Utf8ExecutionLimitKind.None) throw new InvalidOperationException($"UTF-8 subject processing exceeded the invocation {breach} budget.");
        return result;
    }

    internal static Utf8ExecutionLimitKind TrySubject(ReadOnlySpan<byte> input,
        ref Utf8SliceInvocation invocation, out Utf8ValidationResult result)
    {
        if (invocation.InputValidation == Utf8InputValidation.Validate && !invocation.SubjectValidationCompleted)
        {
            Diagnostics.Utf8SearchDiagnosticsSession.Current?.CountSubjectValidation();
            var breach = TryValidate(input, ref invocation, out result);
            if (breach == Utf8ExecutionLimitKind.None) invocation.SubjectValidationCompleted = true;
            return breach;
        }
        return TryDescribeValid(input, ref invocation, out result);
    }

    internal static Utf8ExecutionLimitKind TryCheckSubject(ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation)
    {
        if (invocation.InputValidation == Utf8InputValidation.Validate && !invocation.SubjectValidationCompleted)
        {
            Diagnostics.Utf8SearchDiagnosticsSession.Current?.CountSubjectValidation();
            var breach = TryValidateOnly(input, ref invocation);
            if (breach == Utf8ExecutionLimitKind.None) invocation.SubjectValidationCompleted = true;
            return breach;
        }
        var check = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        return check;
    }

    internal static void CheckSubject(ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation)
    {
        var breach = TryCheckSubject(input, ref invocation);
        if (breach != Utf8ExecutionLimitKind.None) throw new InvalidOperationException($"UTF-8 subject processing exceeded the invocation {breach} budget.");
    }

    internal static bool SubjectIsAscii(ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation)
    {
        if (invocation.InputValidation == Utf8InputValidation.Validate && !invocation.SubjectValidationCompleted)
        {
            Diagnostics.Utf8SearchDiagnosticsSession.Current?.CountSubjectValidation();
            var breach = TryValidate(input, ref invocation, computeUtf16Length: false, out _, out var isAscii);
            if (breach != Utf8ExecutionLimitKind.None) throw new InvalidOperationException($"UTF-8 subject processing exceeded the invocation {breach} budget.");
            invocation.SubjectValidationCompleted = true;
            return isAscii;
        }
        CheckSubject(input, ref invocation);
        if (invocation.IsUnmetered && invocation.CheckpointHook is null) return Utf8InputAnalyzer.IsAscii(input);
        var ascii = true;
        for (var offset = 0; offset < input.Length; offset += Math.Min(2048, input.Length - offset))
        {
            var breach = invocation.TryChargeInputStep();
            invocation.Budget.ThrowIfCancellationRequested();
            if (breach != Utf8ExecutionLimitKind.None) throw new InvalidOperationException($"UTF-8 subject processing exceeded the invocation {breach} budget.");
            ascii &= Utf8InputAnalyzer.IsAscii(input.Slice(offset, Math.Min(2048, input.Length - offset)));
        }
        return ascii;
    }

    // Coordinate/eligibility metadata is still needed by some routes. Derive it
    // under their existing interruption authority, without a strict validity pass.
    internal static Utf8ExecutionLimitKind TryDescribeValid(ReadOnlySpan<byte> input,
        ref Utf8SliceInvocation invocation, out Utf8ValidationResult result)
    {
        result = default;
        var breach = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        if (invocation.IsUnmetered && invocation.CheckpointHook is null)
        {
            result = DescribeValid(input);
            return Utf8ExecutionLimitKind.None;
        }
        var length = 0; var ascii = true; var supplementary = false;
        for (var offset = 0; offset < input.Length;)
        {
            breach = invocation.TryChargeInputStep();
            invocation.Budget.ThrowIfCancellationRequested();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var count = Utf8SliceBufferWork.Utf8BlockLength(input[offset..]);
            var block = DescribeValid(input.Slice(offset, count));
            length += block.Utf16Length; ascii &= block.IsAscii;
            supplementary |= block.ContainsSupplementaryScalars;
            offset += count;
        }
        result = new(input.Length, length, ascii, supplementary);
        return invocation.TryChargeInputStep();
    }
}
