using System.Reflection;
using System.Reflection.Emit;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8EmittedAnchoredValidatorMatcher
{
    private const int MaxSmallPositiveSetSize = 8;

    internal delegate int MatchDelegate(ReadOnlySpan<byte> input);

    private static readonly MethodInfo s_getSpanLengthMethod =
        typeof(ReadOnlySpan<byte>).GetProperty(nameof(ReadOnlySpan<byte>.Length))?.GetMethod ??
        throw new MissingMethodException(typeof(ReadOnlySpan<byte>).FullName, "get_Length");

    private static readonly MethodInfo s_getSpanItemMethod =
        typeof(ReadOnlySpan<byte>).GetProperty("Item")?.GetMethod ??
        throw new MissingMethodException(typeof(ReadOnlySpan<byte>).FullName, "get_Item");

    private readonly MatchDelegate _match;

    private Utf8EmittedAnchoredValidatorMatcher(MatchDelegate match)
    {
        _match = match;
    }

    internal static bool TryCreate(
        AsciiSimplePatternAnchoredValidatorPlan plan,
        bool allowTrailingNewline,
        out Utf8EmittedAnchoredValidatorMatcher? matcher)
    {
        matcher = null;
        if (!CanCreate(plan))
        {
            return false;
        }

        var compiled = Compile(plan, allowTrailingNewline);
        if (!Validate(compiled))
        {
            return false;
        }

        matcher = new Utf8EmittedAnchoredValidatorMatcher(compiled);
        return true;
    }

    internal static bool TryCreate(
        AsciiSimplePatternAnchoredBoundedDatePlan plan,
        bool allowTrailingNewline,
        out Utf8EmittedAnchoredValidatorMatcher? matcher)
    {
        matcher = null;
        if (!plan.HasValue ||
            plan.SecondFieldMinCount == 0 ||
            plan.ThirdFieldMinCount == 0 ||
            plan.FirstFieldMaxCount < plan.FirstFieldMinCount ||
            plan.SecondFieldMaxCount < plan.SecondFieldMinCount ||
            plan.ThirdFieldMaxCount < plan.ThirdFieldMinCount)
        {
            return false;
        }

        var candidateCount =
            (plan.FirstFieldMaxCount - plan.FirstFieldMinCount + 1) *
            (plan.SecondFieldMaxCount - plan.SecondFieldMinCount + 1) *
            (plan.ThirdFieldMaxCount - plan.ThirdFieldMinCount + 1);
        var maximumLength =
            plan.FirstFieldMaxCount +
            plan.SecondFieldMaxCount +
            plan.ThirdFieldMaxCount +
            2;
        if (candidateCount > 32 || maximumLength > 64)
        {
            return false;
        }

        var compiled = CompileBoundedDate();
        if (!Validate(compiled))
        {
            return false;
        }

        matcher = new Utf8EmittedAnchoredValidatorMatcher(compiled);
        return true;

        MatchDelegate CompileBoundedDate()
        {
            var dynamicMethod = new DynamicMethod(
                "Utf8Regex_EmitAnchoredBoundedDateMatch",
                typeof(int),
                [typeof(ReadOnlySpan<byte>)],
                typeof(Utf8EmittedAnchoredValidatorMatcher),
                skipVisibility: false);

            var emitter = new Utf8IlEmitter(dynamicMethod.GetILGenerator(), s_getSpanLengthMethod, s_getSpanItemMethod, inputArgIndex: 0);
            var inputLengthLocal = emitter.DeclareLocal<int>();
            var effectiveLengthLocal = emitter.DeclareLocal<int>();
            var zeroIndexLocal = emitter.DeclareLocal<int>();
            var valueLocal = emitter.DeclareLocal<byte>();

            emitter.LoadInputLength();
            emitter.StoreLocal(inputLengthLocal);
            emitter.LoadLocal(inputLengthLocal);
            emitter.StoreLocal(effectiveLengthLocal);
            emitter.LdcI4(0);
            emitter.StoreLocal(zeroIndexLocal);

            if (allowTrailingNewline)
            {
                emitter.EmitTrimSingleTrailingLf(inputLengthLocal, effectiveLengthLocal, valueLocal);
            }

            for (var firstCount = (int)plan.FirstFieldMaxCount; firstCount >= plan.FirstFieldMinCount; firstCount--)
            {
                for (var secondCount = (int)plan.SecondFieldMaxCount; secondCount >= plan.SecondFieldMinCount; secondCount--)
                {
                    for (var thirdCount = (int)plan.ThirdFieldMaxCount; thirdCount >= plan.ThirdFieldMinCount; thirdCount--)
                    {
                        EmitCandidate(firstCount, secondCount, thirdCount);
                    }
                }
            }

            emitter.EmitReturnInt(-1);
            return dynamicMethod.CreateDelegate<MatchDelegate>();

            void EmitCandidate(int firstCount, int secondCount, int thirdCount)
            {
                var candidateLength = firstCount + secondCount + thirdCount + 2;
                var nextCandidateLabel = emitter.DefineLabel();
                var matchingLengthLabel = emitter.DefineLabel();
                emitter.LoadLocal(effectiveLengthLocal);
                emitter.LdcI4(candidateLength);
                emitter.Emit(OpCodes.Beq, matchingLengthLabel);
                emitter.Emit(OpCodes.Br, nextCandidateLabel);
                emitter.MarkLabel(matchingLengthLabel);

                var offset = 0;
                EmitDigitRun(emitter, zeroIndexLocal, valueLocal, ref offset, firstCount, nextCandidateLabel);
                EmitLiteral(emitter, zeroIndexLocal, valueLocal, ref offset, plan.SeparatorByte, nextCandidateLabel);
                EmitDigitRun(emitter, zeroIndexLocal, valueLocal, ref offset, secondCount, nextCandidateLabel);
                EmitLiteral(emitter, zeroIndexLocal, valueLocal, ref offset, plan.SecondSeparatorByte, nextCandidateLabel);
                EmitDigitRun(emitter, zeroIndexLocal, valueLocal, ref offset, thirdCount, nextCandidateLabel);
                emitter.EmitReturnInt(candidateLength);
                emitter.MarkLabel(nextCandidateLabel);
            }
        }
    }

    internal int MatchWhole(ReadOnlySpan<byte> input) => _match(input);

    private static bool CanCreate(AsciiSimplePatternAnchoredValidatorPlan plan)
    {
        if (!plan.HasValue)
        {
            return false;
        }

        foreach (var segment in plan.Segments)
        {
            if (segment.IsLiteral)
            {
                continue;
            }

            if (segment.MaxLength == int.MaxValue)
            {
                return false;
            }

            if (segment.MinLength != segment.MaxLength)
            {
                return false;
            }

            if (segment.PredicateKind != AsciiCharClassPredicateKind.None)
            {
                continue;
            }

            if (segment.CharClass is not { Negated: false } charClass ||
                charClass.GetPositiveMatchBytes().Length is 0 or > MaxSmallPositiveSetSize)
            {
                return false;
            }
        }

        return true;
    }

    private static MatchDelegate Compile(AsciiSimplePatternAnchoredValidatorPlan plan, bool allowTrailingNewline)
    {
        var dynamicMethod = new DynamicMethod(
            "Utf8Regex_EmitAnchoredValidatorMatch",
            typeof(int),
            [typeof(ReadOnlySpan<byte>)],
            typeof(Utf8EmittedAnchoredValidatorMatcher),
            skipVisibility: false);

        var emitter = new Utf8IlEmitter(dynamicMethod.GetILGenerator(), s_getSpanLengthMethod, s_getSpanItemMethod, inputArgIndex: 0);
        var inputLengthLocal = emitter.DeclareLocal<int>();
        var effectiveLengthLocal = emitter.DeclareLocal<int>();
        var indexLocal = emitter.DeclareLocal<int>();
        var loopIndexLocal = emitter.DeclareLocal<int>();
        var valueLocal = emitter.DeclareLocal<byte>();
        var successLabel = emitter.DefineLabel();
        var failLabel = emitter.DefineLabel();

        emitter.LoadInputLength();
        emitter.StoreLocal(inputLengthLocal);
        emitter.LoadLocal(inputLengthLocal);
        emitter.StoreLocal(effectiveLengthLocal);
        emitter.LdcI4(0);
        emitter.StoreLocal(indexLocal);

        if (allowTrailingNewline)
        {
            emitter.EmitTrimSingleTrailingLf(inputLengthLocal, effectiveLengthLocal, valueLocal);
        }

        foreach (var segment in plan.Segments)
        {
            if (segment.IsLiteral)
            {
                var enoughLiteralLabel = emitter.DefineLabel();
                emitter.EmitEnsureAvailable(indexLocal, effectiveLengthLocal, segment.Literal.Length, enoughLiteralLabel, failLabel);
                emitter.MarkLabel(enoughLiteralLabel);

                if (!plan.IgnoreCase)
                {
                    for (var i = 0; i < segment.Literal.Length; i++)
                    {
                        var nextLiteralLabel = emitter.DefineLabel();
                        emitter.LoadInputByte(indexLocal, i);
                        emitter.StoreLocal(valueLocal);
                        emitter.EmitEqualityBranch(valueLocal, segment.Literal[i], nextLiteralLabel, failLabel);
                        emitter.MarkLabel(nextLiteralLabel);
                    }
                }
                else
                {
                    var literalMatchedLabel = emitter.DefineLabel();
                    emitter.EmitSpanLiteralEqualsIgnoreCase(indexLocal, segment.Literal, 0, valueLocal, literalMatchedLabel, failLabel);
                    emitter.MarkLabel(literalMatchedLabel);
                }

                emitter.EmitStoreSum(indexLocal, indexLocal, segment.Literal.Length);
                continue;
            }

            if (segment.MaxLength == int.MaxValue)
            {
                var enoughTailLabel = emitter.DefineLabel();
                var tailLoopLabel = emitter.DefineLabel();
                var tailContinueLabel = emitter.DefineLabel();
                var tailDoneLabel = emitter.DefineLabel();

                emitter.EmitEnsureAvailable(indexLocal, effectiveLengthLocal, segment.MinLength, enoughTailLabel, failLabel);
                emitter.MarkLabel(enoughTailLabel);
                emitter.LoadLocal(indexLocal);
                emitter.StoreLocal(loopIndexLocal);

                emitter.MarkLabel(tailLoopLabel);
                emitter.EmitLocalLessThanLocalBranch(loopIndexLocal, effectiveLengthLocal, tailContinueLabel, tailDoneLabel);
                emitter.MarkLabel(tailContinueLabel);
                emitter.LoadInputByte(loopIndexLocal, 0);
                emitter.StoreLocal(valueLocal);
                emitter.EmitPredicateBranch(segment.PredicateKind, valueLocal, tailDoneLabel, failLabel);
                emitter.EmitStoreSum(loopIndexLocal, loopIndexLocal, 1);
                emitter.Emit(OpCodes.Br, tailLoopLabel);
                emitter.MarkLabel(tailDoneLabel);
                emitter.LoadLocal(effectiveLengthLocal);
                emitter.StoreLocal(indexLocal);
                continue;
            }

            var enoughRunLabel = emitter.DefineLabel();
            emitter.EmitEnsureAvailable(indexLocal, effectiveLengthLocal, segment.MaxLength, enoughRunLabel, failLabel);
            emitter.MarkLabel(enoughRunLabel);
            for (var i = 0; i < segment.MaxLength; i++)
            {
                var nextRunLabel = emitter.DefineLabel();
                emitter.LoadInputByte(indexLocal, i);
                emitter.StoreLocal(valueLocal);
                if (segment.PredicateKind != AsciiCharClassPredicateKind.None)
                {
                    emitter.EmitPredicateBranch(segment.PredicateKind, valueLocal, nextRunLabel, failLabel);
                }
                else
                {
                    emitter.EmitSmallPositiveSetBranch(segment.CharClass.GetPositiveMatchBytes(), valueLocal, nextRunLabel, failLabel);
                }

                emitter.MarkLabel(nextRunLabel);
            }

            emitter.EmitStoreSum(indexLocal, indexLocal, segment.MaxLength);
        }

        emitter.EmitLocalsEqualBranch(indexLocal, effectiveLengthLocal, successLabel, failLabel);

        emitter.MarkLabel(successLabel);
        emitter.EmitReturnLocal(indexLocal);

        emitter.MarkLabel(failLabel);
        emitter.EmitReturnInt(-1);
        return dynamicMethod.CreateDelegate<MatchDelegate>();
    }

    private static void EmitDigitRun(
        Utf8IlEmitter emitter,
        LocalBuilder zeroIndexLocal,
        LocalBuilder valueLocal,
        ref int offset,
        int count,
        Label failureLabel)
    {
        for (var i = 0; i < count; i++)
        {
            var matchedLabel = emitter.DefineLabel();
            emitter.LoadInputByte(zeroIndexLocal, offset++);
            emitter.StoreLocal(valueLocal);
            emitter.EmitPredicateBranch(AsciiCharClassPredicateKind.Digit, valueLocal, matchedLabel, failureLabel);
            emitter.MarkLabel(matchedLabel);
        }
    }

    private static void EmitLiteral(
        Utf8IlEmitter emitter,
        LocalBuilder zeroIndexLocal,
        LocalBuilder valueLocal,
        ref int offset,
        byte literal,
        Label failureLabel)
    {
        var matchedLabel = emitter.DefineLabel();
        emitter.LoadInputByte(zeroIndexLocal, offset++);
        emitter.StoreLocal(valueLocal);
        emitter.EmitEqualityBranch(valueLocal, literal, matchedLabel, failureLabel);
        emitter.MarkLabel(matchedLabel);
    }

    private static bool Validate(MatchDelegate match)
    {
        try
        {
            _ = match(ReadOnlySpan<byte>.Empty);
            _ = match([0]);
            _ = match("A0f"u8);
            _ = match("AB0f"u8);
            return true;
        }
        catch (InvalidProgramException)
        {
            return false;
        }
    }
}
