using System.Reflection;
using System.Reflection.Emit;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>
/// Emits bounded exact-subject recognizers. Callers may accept a successful result directly,
/// but must preserve the ordinary search and UTF-8 validation paths when the recognizer misses.
/// </summary>
internal sealed partial class Utf8EmittedWholeMatcher
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

    private Utf8EmittedWholeMatcher(MatchDelegate match)
    {
        _match = match;
    }

    internal static bool TryCreate(
        AsciiSimplePatternAnchoredValidatorPlan plan,
        bool allowTrailingNewline,
        out Utf8EmittedWholeMatcher? matcher)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
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

            matcher = budget.AllocateObject(compiled, static value => new Utf8EmittedWholeMatcher(value));
            scope.Complete();
            return true;
        }
        finally
        {
            budget.LeaveDepth();
        }
    }

    internal static bool TryCreate(
        AsciiSimplePatternAnchoredBoundedDatePlan plan,
        bool allowTrailingNewline,
        out Utf8EmittedWholeMatcher? matcher)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
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

            matcher = budget.AllocateObject(compiled, static value => new Utf8EmittedWholeMatcher(value));
            scope.Complete();
            return true;

            MatchDelegate CompileBoundedDate()
            {
                using var dynamicMethod = Utf8DynamicIlBuilder.Create("Utf8Regex_EmitAnchoredBoundedDateMatch", typeof(int), typeof(Utf8EmittedWholeMatcher), typeof(ReadOnlySpan<byte>));

                var emitter = new Utf8IlEmitter(dynamicMethod, s_getSpanLengthMethod, s_getSpanItemMethod, inputArgIndex: 0);
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
        finally
        {
            budget.LeaveDepth();
        }
    }

    internal static bool TryCreate(
        AsciiSimplePatternAnchoredOptionalFieldPlan plan,
        bool allowTrailingNewline,
        out Utf8EmittedWholeMatcher? matcher)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            matcher = null;
            var candidateCount = (plan.HeadMaxCount - plan.HeadMinCount + 1) * 4;
            var maximumLength = plan.HeadMaxCount + plan.TailCount + 4;
            if (!plan.HasValue ||
                candidateCount > 32 ||
                maximumLength > 64 ||
                !CanEmit(plan.HeadClass) ||
                !CanEmit(plan.FirstRequiredClass) ||
                !CanEmit(plan.OptionalClass) ||
                !CanEmit(plan.SecondRequiredClass) ||
                !CanEmit(plan.TailClass))
            {
                return false;
            }

            var compiled = CompileOptionalField();
            if (!Validate(compiled))
            {
                return false;
            }

            matcher = budget.AllocateObject(compiled, static value => new Utf8EmittedWholeMatcher(value));
            scope.Complete();
            return true;

            static bool CanEmit(AsciiCharClass charClass)
            {
                if (charClass.TryGetKnownPredicateKind(out var predicateKind) &&
                    predicateKind != AsciiCharClassPredicateKind.None)
                {
                    return true;
                }

                return charClass is { Negated: false } &&
                    charClass.Count is > 0 and <= MaxSmallPositiveSetSize;
            }

            MatchDelegate CompileOptionalField()
            {
                using var dynamicMethod = Utf8DynamicIlBuilder.Create("Utf8Regex_EmitAnchoredOptionalFieldMatch", typeof(int), typeof(Utf8EmittedWholeMatcher), typeof(ReadOnlySpan<byte>));

                var emitter = new Utf8IlEmitter(dynamicMethod, s_getSpanLengthMethod, s_getSpanItemMethod, inputArgIndex: 0);
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

                for (var headCount = (int)plan.HeadMaxCount; headCount >= plan.HeadMinCount; headCount--)
                {
                    EmitCandidate(headCount, hasOptionalClass: true, hasOptionalLiteral: true);
                    EmitCandidate(headCount, hasOptionalClass: true, hasOptionalLiteral: false);
                    EmitCandidate(headCount, hasOptionalClass: false, hasOptionalLiteral: true);
                    EmitCandidate(headCount, hasOptionalClass: false, hasOptionalLiteral: false);
                }

                emitter.EmitReturnInt(-1);
                return dynamicMethod.CreateDelegate<MatchDelegate>();

                void EmitCandidate(int headCount, bool hasOptionalClass, bool hasOptionalLiteral)
                {
                    var candidateLength = headCount + plan.TailCount + 2 +
                        (hasOptionalClass ? 1 : 0) +
                        (hasOptionalLiteral ? 1 : 0);
                    var nextCandidateLabel = emitter.DefineLabel();
                    var matchingLengthLabel = emitter.DefineLabel();
                    emitter.LoadLocal(effectiveLengthLocal);
                    emitter.LdcI4(candidateLength);
                    emitter.Emit(OpCodes.Beq, matchingLengthLabel);
                    emitter.Emit(OpCodes.Br, nextCandidateLabel);
                    emitter.MarkLabel(matchingLengthLabel);

                    var offset = 0;
                    for (var i = 0; i < headCount; i++)
                    {
                        EmitCharClass(plan.HeadClass, ref offset, nextCandidateLabel);
                    }

                    EmitCharClass(plan.FirstRequiredClass, ref offset, nextCandidateLabel);
                    if (hasOptionalClass)
                    {
                        EmitCharClass(plan.OptionalClass, ref offset, nextCandidateLabel);
                    }

                    if (hasOptionalLiteral)
                    {
                        EmitLiteral(emitter, zeroIndexLocal, valueLocal, ref offset, plan.OptionalLiteral, nextCandidateLabel);
                    }

                    EmitCharClass(plan.SecondRequiredClass, ref offset, nextCandidateLabel);
                    for (var i = 0; i < plan.TailCount; i++)
                    {
                        EmitCharClass(plan.TailClass, ref offset, nextCandidateLabel);
                    }

                    emitter.EmitReturnInt(candidateLength);
                    emitter.MarkLabel(nextCandidateLabel);
                }

                void EmitCharClass(AsciiCharClass charClass, ref int offset, Utf8IlLabel failureLabel)
                {
                    var matchedLabel = emitter.DefineLabel();
                    emitter.LoadInputByte(zeroIndexLocal, offset++);
                    emitter.StoreLocal(valueLocal);
                    if (charClass.TryGetKnownPredicateKind(out var predicateKind) &&
                        predicateKind != AsciiCharClassPredicateKind.None)
                    {
                        emitter.EmitPredicateBranch(predicateKind, valueLocal, matchedLabel, failureLabel);
                    }
                    else
                    {
                        emitter.EmitSmallPositiveSetBranch(charClass, valueLocal, matchedLabel, failureLabel);
                    }

                    emitter.MarkLabel(matchedLabel);
                }
            }
        }
        finally { budget.LeaveDepth(); }
    }

    internal static bool TryCreate(
        AsciiSimplePatternRepeatedDigitGroupPlan plan,
        out Utf8EmittedWholeMatcher? matcher)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            matcher = null;
            if (!plan.HasValue ||
                plan.TrailingMaxDigits < plan.TrailingMinDigits ||
                plan.RepeatedGroupCount > 4 ||
                plan.GroupDigitCount > 4 ||
                plan.TrailingMaxDigits > 4 ||
                plan.SeparatorBytes.Length is 0 or > MaxSmallPositiveSetSize)
            {
                return false;
            }

            var candidateCount = plan.TrailingMaxDigits - plan.TrailingMinDigits + 1;
            if (candidateCount > 32 || plan.MaximumLength > 64)
            {
                return false;
            }

            var compiled = CompileRepeatedDigitGroup();
            if (!Validate(compiled))
            {
                return false;
            }

            matcher = budget.AllocateObject(compiled, static value => new Utf8EmittedWholeMatcher(value));
            scope.Complete();
            return true;

            MatchDelegate CompileRepeatedDigitGroup()
            {
                using var dynamicMethod = Utf8DynamicIlBuilder.Create("Utf8Regex_EmitRepeatedDigitGroupMatch", typeof(int), typeof(Utf8EmittedWholeMatcher), typeof(ReadOnlySpan<byte>));

                var emitter = new Utf8IlEmitter(dynamicMethod, s_getSpanLengthMethod, s_getSpanItemMethod, inputArgIndex: 0);
                var inputLengthLocal = emitter.DeclareLocal<int>();
                var zeroIndexLocal = emitter.DeclareLocal<int>();
                var valueLocal = emitter.DeclareLocal<byte>();

                emitter.LoadInputLength();
                emitter.StoreLocal(inputLengthLocal);
                emitter.LdcI4(0);
                emitter.StoreLocal(zeroIndexLocal);

                for (var trailingCount = (int)plan.TrailingMaxDigits; trailingCount >= plan.TrailingMinDigits; trailingCount--)
                {
                    var candidateLength = plan.RepeatedGroupCount * (plan.GroupDigitCount + 1) + trailingCount;
                    var nextCandidateLabel = emitter.DefineLabel();
                    var matchingLengthLabel = emitter.DefineLabel();
                    emitter.LoadLocal(inputLengthLocal);
                    emitter.LdcI4(candidateLength);
                    emitter.Emit(OpCodes.Beq, matchingLengthLabel);
                    emitter.Emit(OpCodes.Br, nextCandidateLabel);
                    emitter.MarkLabel(matchingLengthLabel);

                    var offset = 0;
                    for (var group = 0; group < plan.RepeatedGroupCount; group++)
                    {
                        EmitDigitRun(emitter, zeroIndexLocal, valueLocal, ref offset, plan.GroupDigitCount, nextCandidateLabel);
                        EmitSeparator(ref offset, nextCandidateLabel);
                    }

                    EmitDigitRun(emitter, zeroIndexLocal, valueLocal, ref offset, trailingCount, nextCandidateLabel);
                    emitter.EmitReturnInt(candidateLength);
                    emitter.MarkLabel(nextCandidateLabel);
                }

                emitter.EmitReturnInt(-1);
                return dynamicMethod.CreateDelegate<MatchDelegate>();

                void EmitSeparator(ref int offset, Utf8IlLabel failureLabel)
                {
                    var matchedLabel = emitter.DefineLabel();
                    emitter.LoadInputByte(zeroIndexLocal, offset++);
                    emitter.StoreLocal(valueLocal);
                    emitter.EmitSmallPositiveSetBranch(plan.SeparatorBytes, valueLocal, matchedLabel, failureLabel);
                    emitter.MarkLabel(matchedLabel);
                }
            }
        }
        finally { budget.LeaveDepth(); }
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
            Utf8ConstructionScope.ChargeWork(1);
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
                charClass.Count is 0 or > MaxSmallPositiveSetSize)
            {
                return false;
            }
        }

        return true;
    }

    private static MatchDelegate Compile(AsciiSimplePatternAnchoredValidatorPlan plan, bool allowTrailingNewline)
    {
        using var dynamicMethod = Utf8DynamicIlBuilder.Create("Utf8Regex_EmitAnchoredValidatorMatch", typeof(int), typeof(Utf8EmittedWholeMatcher), typeof(ReadOnlySpan<byte>));

        var emitter = new Utf8IlEmitter(dynamicMethod, s_getSpanLengthMethod, s_getSpanItemMethod, inputArgIndex: 0);
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
                    emitter.EmitSmallPositiveSetBranch(segment.CharClass, valueLocal, nextRunLabel, failLabel);
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
        Utf8IlLocal zeroIndexLocal,
        Utf8IlLocal valueLocal,
        ref int offset,
        int count,
        Utf8IlLabel failureLabel)
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
        Utf8IlLocal zeroIndexLocal,
        Utf8IlLocal valueLocal,
        ref int offset,
        byte literal,
        Utf8IlLabel failureLabel)
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
            Utf8ConstructionScope.ChargeWork(10);
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
