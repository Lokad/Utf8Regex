using System.Reflection;
using System.Reflection.Emit;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8EmittedAnchoredValidatorMatcher
{
    private const int MaxSmallPositiveSetSize = 8;

    internal delegate int MatchDelegate(ReadOnlySpan<byte> input);

    private static readonly MethodInfo s_getSpanLengthMethod =
        typeof(ReadOnlySpan<byte>).GetProperty(nameof(ReadOnlySpan<byte>.Length))!.GetMethod!;

    private static readonly MethodInfo s_getSpanItemMethod =
        typeof(ReadOnlySpan<byte>).GetProperty("Item")!.GetMethod!;

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
                    emitter.EmitSmallPositiveSetBranch(segment.CharClass!.GetPositiveMatchBytes(), valueLocal, nextRunLabel, failLabel);
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
